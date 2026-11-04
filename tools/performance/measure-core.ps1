#requires -Version 7.5
[CmdletBinding()]
param(
    [string]$ProjectPath=(Join-Path $PSScriptRoot '../..'),
    [Parameter(Mandatory)][string]$NUnitPath,
    [ValidatePattern('^[A-Za-z0-9_-]{1,32}$')][string]$Label='baseline',
    [ValidateRange(1000,3000)][int]$Repetitions=2000,
    [ValidateRange(1000,10000)][int]$Warmup=2000
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$ownRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
if (-not $root.Equals($ownRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'ProjectPath must identify this checkout.' }
$nunit=[IO.Path]::GetFullPath($NUnitPath)
if (-not (Test-Path -LiteralPath $nunit -PathType Leaf)) { throw 'NUnitPath must identify an existing nunit.framework.dll.' }
. (Join-Path $root 'tools/validation/source-state.ps1')
$runId=[guid]::NewGuid().ToString('N')
$runDirectory=Join-Path $root ('Logs/Validation/session-c-'+$runId)
[void][IO.Directory]::CreateDirectory($runDirectory)
$reportPath=Join-Path $runDirectory 'report.json'
$manifest=[ordered]@{
    schemaVersion=1;runId=$runId;label=$Label;startedUtc=[DateTime]::UtcNow.ToString('O');completedUtc=''
    projectPath=$root;sourceHash=(Get-ValidationSourceHash $root);sourceHashAfter='';gitHead=(& git --no-optional-locks -C $root rev-parse HEAD)
    runtimeTarget='net10.0';repetitions=$Repetitions;warmup=$Warmup;batches=5
    fixtureNames=@();excludedOptionalFixtures=@();compiledSources=@();probeSources=@();nunitSha256=(Get-FileHash -LiteralPath $nunit).Hash.ToLowerInvariant()
    status='Pending';failureKind='';message='';reportPath=$reportPath
}
$manifestPath=Join-Path $runDirectory 'manifest.json'
$savedEnvironment=@{}
try {
    $sources=[Collections.Generic.List[string]]::new()
    foreach ($directory in @('Assets/_SsalMuk/Core','Assets/_SsalMuk/Presentation')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $directory) -Recurse -File -Filter '*.cs') { $sources.Add($file.FullName) }
    }
    $sources.Add((Join-Path $root 'Assets/_SsalMuk/Tests/Shared/RunTestRig.cs'))
    $sources.Add((Join-Path $PSScriptRoot 'CoreProbe.cs'))
    foreach ($fixture in @('WorldStoreTests','ExperiencePickupTests','WorldPresentationTests','SessionCPerformanceTests','SessionCWorldPresentationTests')) {
        $file=Join-Path $root ('Assets/_SsalMuk/Tests/EditMode/'+$fixture+'.cs')
        if (Test-Path -LiteralPath $file -PathType Leaf) { $sources.Add($file); $manifest.fixtureNames+=('SsalMuk.Tests.'+$fixture) }
        elseif ($fixture -like 'SessionC*' -and $Label -ne 'after') { $manifest.excludedOptionalFixtures+=$fixture }
        else { throw "Required fixture is missing: $fixture" }
    }
    foreach ($file in $sources) {
        $manifest.compiledSources+=@{path=$file.Substring($root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()}
    }
    foreach ($file in @($PSCommandPath,(Join-Path $PSScriptRoot 'CoreProbe.cs'))) {
        $manifest.probeSources+=@{path=$file.Substring($root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()}
    }
    Write-ValidationJson $manifestPath $manifest
    $compile=($sources | ForEach-Object { '    <Compile Include="'+[Security.SecurityElement]::Escape($_)+'" />' }) -join "`n"
    $reference=[Security.SecurityElement]::Escape($nunit)
    $project=@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable><Optimize>true</Optimize><TieredCompilation>false</TieredCompilation>
    <ServerGarbageCollection>false</ServerGarbageCollection><NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
$compile
    <Reference Include="nunit.framework"><HintPath>$reference</HintPath><Private>true</Private></Reference>
  </ItemGroup>
</Project>
"@
    $projectFile=Join-Path $runDirectory 'CoreProbe.csproj'
    [IO.File]::WriteAllText($projectFile,$project,[Text.UTF8Encoding]::new($false))
    $nugetConfig=Join-Path $runDirectory 'NuGet.Config'
    [IO.File]::WriteAllText($nugetConfig,'<configuration><packageSources><clear /></packageSources></configuration>')
    $environment=@{DOTNET_CLI_HOME=(Join-Path $runDirectory '.dotnet');NUGET_PACKAGES=(Join-Path $runDirectory 'packages');DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_NOLOGO='1'}
    foreach ($name in $environment.Keys) { $savedEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process'); [Environment]::SetEnvironmentVariable($name,$environment[$name],'Process') }
    & dotnet build $projectFile --configuration Release --nologo --verbosity quiet --configfile $nugetConfig *> (Join-Path $runDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { $manifest.failureKind='CompileFailed'; throw "Core probe compilation failed; see $(Join-Path $runDirectory 'build.log')." }
    if ((Get-ValidationSourceHash $root) -ne $manifest.sourceHash) { $manifest.failureKind='SourceChanged'; throw 'Source changed during compilation.' }
    & dotnet (Join-Path $runDirectory 'bin/Release/net10.0/CoreProbe.dll') $manifestPath $reportPath *> (Join-Path $runDirectory 'probe.log')
    $probeExitCode=$LASTEXITCODE
    if (-not (Test-Path -LiteralPath $reportPath)) { $manifest.failureKind='MissingReport'; throw 'Core probe did not write report.json.' }
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $manifest.sourceHashAfter=Get-ValidationSourceHash $root
    if ($manifest.sourceHashAfter -ne $manifest.sourceHash) { $manifest.failureKind='SourceChanged'; throw 'Source changed during measurement.' }
    foreach ($entry in $manifest.compiledSources) {
        if ((Get-FileHash -LiteralPath (Join-Path $root $entry.path)).Hash.ToLowerInvariant() -ne $entry.sha256) { $manifest.failureKind='SourceChanged'; throw "Compiled source changed: $($entry.path)" }
    }
    $manifest.status=if ($probeExitCode -eq 0) { 'Passed' } else { 'Failed' }
    if ($probeExitCode -ne 0) { $manifest.failureKind='ProbeFailed'; $manifest.message='See test and measurement failures in report.json.' }
    $manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    Write-ValidationJson $manifestPath $manifest
    Write-Output ("{0}: tests {1}/{2}, failed {3}; {4}" -f $Label,$report.tests.passed,$report.tests.total,$report.tests.failed,$reportPath)
    exit $probeExitCode
}
catch {
    $manifest.status='Failed';$manifest.message=$_.Exception.Message;$manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    if (-not $manifest.failureKind) { $manifest.failureKind='RunnerError' }
    Write-ValidationJson $manifestPath $manifest
    Write-Error -Message $manifest.message -ErrorAction Continue
    exit 1
}
finally { foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process') } }
