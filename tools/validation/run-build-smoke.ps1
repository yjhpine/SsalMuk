#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildRunId,
    [ValidateSet('EndToEnd','RepeatedRestart','CrowdCorridor','PersistentWorld10m','PersistentWorld30m','HighGrowth')][string]$Scenario='EndToEnd',
    [ValidateRange(1,43200)][int]$TimeoutSeconds=180
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'read-results.ps1')
. (Join-Path $PSScriptRoot 'source-state.ps1')
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
if ($BuildRunId -cnotmatch '^[a-f0-9]{32}$') { throw 'Invalid build run ID.' }
$build=Read-ValidationJson (Join-Path $root ('Logs/Validation/'+$BuildRunId+'/build.json'))
$hash=Get-ValidationSourceHash $root
if ($build.status -cne 'Passed' -or $build.result -cne 'Succeeded' -or $build.errors -ne 0 -or $build.sourceHash -cne $hash -or -not $build.development -or $build.buildSettingsBefore -cne $build.buildSettingsAfter) { throw 'Build evidence is not a successful current development build.' }
if (@($build.preservedFiles).Count -eq 0 -or @($build.preservedFiles | Where-Object { $_.beforeHash -cnotmatch '^[a-f0-9]{64}$' -or $_.beforeHash -cne $_.afterHash }).Count -ne 0) { throw 'Build did not preserve its input settings.' }
$exe=[IO.Path]::GetFullPath((Join-Path $root 'Builds/SsalMuk/SsalMuk.exe'))
if ([IO.Path]::GetFullPath($build.executable) -cne $exe -or (Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant() -cne $build.executableSha256) { throw 'Executable differs from the verified build.' }
$id=[guid]::NewGuid().ToString('N'); $output=Join-Path $root ('Logs/Validation/'+$id); [IO.Directory]::CreateDirectory($output) | Out-Null
$manifest=[ordered]@{schemaVersion=1;runId=$id;sourceHash=$hash;scenario=$Scenario;buildRunId=$BuildRunId;executableSha256=$build.executableSha256;startedUtc=[DateTime]::UtcNow.ToString('O');completedUtc='';status='Pending';message='';timeoutSeconds=$TimeoutSeconds;processId=0}
Write-ValidationJson (Join-Path $output 'standalone-manifest.json') $manifest
$arguments=@('-ssalmukScenario',$Scenario,'-ssalmukOutput',('"'+$output+'"'),'-ssalmukRun',$id,'-ssalmukSource',$hash,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',('"'+(Join-Path $output 'player.log')+'"'))
$player=$null
try {
    $player=Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $manifest.processId=$player.Id
    Write-ValidationJson (Join-Path $output 'standalone-manifest.json') $manifest
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $player.HasExited) {
        $cancel=Read-ValidationJson (Join-Path $output 'cancel.json') -IfAvailable
        if ($null -ne $cancel -and $cancel.runId -ceq $id) { throw 'Standalone scenario cancelled.' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Standalone scenario timed out.' }
        Start-Sleep -Milliseconds 300
    }
    if ($player.ExitCode -ne 0) { throw "Standalone exited with code $($player.ExitCode)." }
    $report=Read-ValidationJson (Join-Path $output 'scenario.json')
    Assert-ScenarioReport $report $id $hash $Scenario
    if ($report.environment -cne 'WindowsPlayer' -or $report.unityVersion -cne $build.unityVersion -or [DateTimeOffset]::Parse($report.startedUtc) -lt [DateTimeOffset]::Parse($manifest.startedUtc)) { throw 'Evidence is not from this standalone execution.' }
    if ((Get-ValidationSourceHash $root) -cne $hash) { throw 'Sources changed during standalone verification.' }
    $manifest.status='Passed'; $manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    Write-ValidationJson (Join-Path $output 'standalone-manifest.json') $manifest
    [pscustomobject]@{status='Passed';mode='WindowsPlayer';runId=$id;results=$output} | ConvertTo-Json -Compress
    exit 0
} catch {
    $manifest.status='Failed'; $manifest.message=$_.Exception.Message; $manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    Write-ValidationJson (Join-Path $output 'standalone-manifest.json') $manifest
    [pscustomobject]@{status='Failed';mode='WindowsPlayer';runId=$id;message=$manifest.message;results=$output} | ConvertTo-Json -Compress
    exit 1
} finally {
    if ($null -ne $player) {
        if (-not $player.HasExited) { $player.Kill(); $player.WaitForExit(5000) | Out-Null }
        $player.Dispose()
    }
}
