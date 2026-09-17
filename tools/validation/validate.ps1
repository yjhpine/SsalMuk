#requires -Version 7.5
[CmdletBinding()]
param(
    [ValidateSet('Static','EditMode','PlayMode','Smoke','Stress')][string]$Mode='Static',
    [string]$Filter='', [string]$Scenario='',
    [string]$ProjectPath=(Join-Path $PSScriptRoot '../..'),
    [ValidateRange(1,14400)][int]$TimeoutSeconds=180,
    [string]$UnityPath=''
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'read-results.ps1')
. (Join-Path $PSScriptRoot 'source-state.ps1')

$root=[IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$ownRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
if (-not $root.Equals($ownRoot,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'ProjectPath must identify this SsalMuk checkout.'
}
if ($Filter -and $Filter -cnotmatch '^[A-Za-z_][A-Za-z0-9_.]{0,255}$') { throw 'Filter must be a qualified namespace, fixture, or method prefix.' }
$runId=[guid]::NewGuid().ToString('N')
$runDirectory=Join-Path $root ('Logs/Validation/'+$runId)
New-Item -ItemType Directory -Path $runDirectory -ErrorAction Stop | Out-Null
$versionLine=Get-Content -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Encoding UTF8 | Select-Object -First 1
$version=($versionLine -split ': ',2)[1]
$manifest=[ordered]@{
    schemaVersion=1;runId=$runId;projectPath=$root.Replace('\','/');mode=$Mode;filter=$Filter;scenario=$Scenario
    startedUtc=[DateTime]::UtcNow.ToString('O');completedUtc='';sourceHash=(Get-ValidationSourceHash $root)
    unityVersion=$version;gitHead=(& git -C $root rev-parse HEAD);timeoutSeconds=$TimeoutSeconds
    packages=(Get-Content -LiteralPath (Join-Path $root 'Packages/manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).dependencies
    status='Pending';failureKind='';message=''
}
$manifestPath=Join-Path $runDirectory 'manifest.json'
Write-ValidationJson $manifestPath $manifest
$requestPath=Join-Path $runDirectory 'request.json'
$receiptPath=Join-Path $runDirectory 'receipt.json'
$batchProcess=$null
$failureKind='RunnerError'
try {
    if ($Mode -in @('Smoke','Stress')) {
        $failureKind='InvalidRequest'
        $allowed=if ($Mode -eq 'Smoke') { @('EndToEnd','RepeatedRestart') } else { @('CrowdCorridor','PersistentWorld10m','PersistentWorld30m','HighGrowth') }
        if ($Scenario -cnotin $allowed -or $Filter) { throw 'Supply one scenario belonging to this mode and no test filter.' }
    }
    elseif ($Scenario) { $failureKind='InvalidRequest'; throw 'Scenario is only supported by Smoke/Stress.' }
    if ($Mode -eq 'Static') {
        $failureKind='StaticChecksFailed'
        & (Join-Path $PSScriptRoot 'tests/result-reader.tests.ps1') | Set-Content -LiteralPath (Join-Path $runDirectory 'static.txt') -Encoding UTF8
        $parseFiles=Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -Filter '*.ps1' -File
        foreach ($file in $parseFiles) {
            $parseTokens=$null; $parseErrors=$null
            [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$parseTokens,[ref]$parseErrors)
            if ($parseErrors.Count -gt 0) { throw "PowerShell parse error: $($file.Name)" }
        }
        $receipt=[ordered]@{schemaVersion=1;runId=$runId;sourceHash=$manifest.sourceHash;projectPath=$manifest.projectPath;mode=$Mode;filter=$Filter;requestedUtc=$manifest.startedUtc;startedUtc=$manifest.startedUtc;completedUtc=[DateTime]::UtcNow.ToString('O');status='Passed';errorCount=0;discoveredNames=@();xmlSha256='';unityVersion=$version}
        Write-ValidationJson $receiptPath $receipt
    } else {
        $request=[ordered]@{schemaVersion=1;runId=$runId;projectPath=$manifest.projectPath;sourceHash=$manifest.sourceHash;mode=$Mode;filter=$Filter;scenario=$Scenario;startedUtc=$manifest.startedUtc;timeoutSeconds=$TimeoutSeconds}
        $instancePath=Join-Path $root 'Library/EditorInstance.json'
        $editorOpen=$false
        if (Test-Path -LiteralPath $instancePath) {
            $instance=Get-Content -LiteralPath $instancePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $editor=Get-Process -Id $instance.process_id -ErrorAction SilentlyContinue
            if ($null -ne $editor -and $editor.ProcessName -eq 'Unity') { $editorOpen=$true }
        }
        if (-not $editorOpen) {
            $lockPath=Join-Path $root 'Temp/UnityLockfile'
            if (Test-Path -LiteralPath $lockPath) {
                try { $probe=[IO.File]::Open($lockPath,'Open','ReadWrite','None'); $probe.Dispose() }
                catch { $failureKind='EditorOwnershipUnknown'; throw 'The project is locked; a second Editor will not be started.' }
            }
            if (-not $UnityPath) { $UnityPath="C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe" }
            if (-not (Test-Path -LiteralPath $UnityPath)) { $failureKind='EditorNotFound'; throw 'The configured Unity Editor does not exist.' }
        }
        Write-ValidationJson $requestPath $request
        if (-not $editorOpen) {
            $batchArguments=@('-batchmode','-projectPath',('"'+$root+'"'),'-executeMethod','SsalMuk.Editor.Validation.EditorValidationBridge.ExecuteBatch','-ssalmukValidationRun',$runId,'-logFile',('"'+(Join-Path $runDirectory 'editor.log')+'"'))
            $batchProcess=Start-Process -FilePath $UnityPath -ArgumentList $batchArguments -WindowStyle Hidden -PassThru
        }
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds+10)
        while (-not (Test-Path -LiteralPath $receiptPath)) {
            if ($null -ne $batchProcess -and $batchProcess.HasExited) { $failureKind='EditorExited'; throw 'Editor exited without a completed validation receipt.' }
            $statusPath=Join-Path $root 'Logs/Validation/editor-status.json'
            if (Test-Path -LiteralPath $statusPath) {
                $state=Read-ValidationJson $statusPath -IfAvailable
                if ($null -ne $state -and [DateTimeOffset]::Parse($state.updatedUtc) -ge [DateTimeOffset]::Parse($manifest.startedUtc) -and $state.compilationFailed) {
                    $failureKind='CompilationFailed'; throw 'Unity reports script compilation errors.'
                }
            }
            if ([DateTime]::UtcNow -gt $deadline) { $failureKind='Timeout'; throw 'Unity validation did not produce a receipt within the time limit.' }
            Start-Sleep -Milliseconds 300
        }
    }
    $manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    Write-ValidationJson $manifestPath $manifest
    $failureKind='InvalidEvidence'
    $candidate=Read-ValidationJson $receiptPath
    if ($candidate.status -ne 'Passed') {
        $failureKind=if ($candidate.failureKind) { $candidate.failureKind } else { 'TestsFailed' }
        throw $candidate.message
    }
    $receipt=Read-ValidationResult $runDirectory (Get-ValidationSourceHash $root)
    $manifest.status='Passed'
    Write-ValidationJson $manifestPath $manifest
    [pscustomobject]@{status='Passed';mode=$Mode;runId=$runId;results=$runDirectory;testCount=@($receipt.discoveredNames).Count} | ConvertTo-Json -Compress
    exit 0
} catch {
    $manifest.status='Failed'; $manifest.failureKind=$failureKind; $manifest.message=$_.Exception.Message
    $manifest.completedUtc=[DateTime]::UtcNow.ToString('O')
    Write-ValidationJson $manifestPath $manifest
    Write-ValidationJson (Join-Path $runDirectory 'cancel.json') @{runId=$runId;reason=$manifest.message}
    # Only withdraw this request. Never stop the user's Editor or remove other runs.
    if (Test-Path -LiteralPath $requestPath) { Move-Item -LiteralPath $requestPath -Destination (Join-Path $runDirectory 'cancelled-request.json') }
    [pscustomobject]@{status='Failed';kind=$failureKind;message=$manifest.message;results=$runDirectory} | ConvertTo-Json -Compress
    exit 1
}
