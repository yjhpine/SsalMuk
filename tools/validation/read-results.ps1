#requires -Version 7.5
Set-StrictMode -Version Latest

function Assert-ScenarioReport {
    param($Report, [string]$RunId, [string]$SourceHash, [string]$Scenario)
    Assert-EvidenceFields $Report @('schemaVersion','runId','sourceHash','scenario','status','errorCount','startedUtc','completedUtc','unityVersion','environment','processor','graphics','operatingSystem','systemMemoryMB','width','height','seed','preset','executionMode','steps','simulationSeconds','observations','samples','metrics')
    if ($Report.schemaVersion -ne 1 -or $Report.runId -cne $RunId -or $Report.sourceHash -cne $SourceHash -or $Report.scenario -cne $Scenario -or $Report.status -cne 'Passed' -or $Report.errorCount -ne 0) { throw 'Runtime identity or status mismatch.' }
    if ($RunId -cnotmatch '^[a-f0-9]{32}$' -or $SourceHash -cnotmatch '^[a-f0-9]{64}$') { throw 'Invalid runtime identity.' }
    if ([DateTimeOffset]::Parse($Report.completedUtc) -lt [DateTimeOffset]::Parse($Report.startedUtc)) { throw 'Reversed scenario timestamps.' }
    foreach ($field in @('unityVersion','processor','graphics','operatingSystem','preset','executionMode')) { if ([string]::IsNullOrWhiteSpace($Report.$field)) { throw "Missing runtime context: $field" } }
    if ($Report.environment -cnotin @('Editor','WindowsPlayer') -or $Report.width -le 0 -or $Report.height -le 0 -or $Report.systemMemoryMB -le 0) { throw 'Invalid runtime environment.' }
    $required = switch -CaseSensitive ($Scenario) {
        'EndToEnd' { @('GameStart','Attack','ExperienceGrounded','ExperienceAttracting','ExperienceFlight','ExperienceContact','ChoiceButton','Death','Results','Restart','CleanScope') }
        'RepeatedRestart' { @('GameStart','ExperienceContact','ChoiceButton','Death','Results','Restart','CleanScope','RestartCycles=3') }
        'PersistentWorld10m' { @('ExperienceBoundaryFlight','AllFixedTicks','NoLivingEntityDeletion','FarGroundProgress','FarAirProgress','FarExperiencePreserved','OverlappingBosses','ViewRoundTrip','Glow600Views') }
        'PersistentWorld30m' { @('ExperienceBoundaryFlight','AllFixedTicks','NoLivingEntityDeletion','FarGroundProgress','FarAirProgress','FarExperiencePreserved','OverlappingBosses','ViewRoundTrip','Glow600Views') }
        'CrowdCorridor' { @('Crowd72','BodySafeCorridor','NoTeleport','AllFixedTicks') }
        'HighGrowth' { @('BigIntegerExact','NumericLimitExplicit','RuntimeTier=0','RuntimeTier=2','RuntimeTier=8','NoAttackOmission') }
        default { throw 'Unknown runtime scenario.' }
    }
    foreach ($name in $required) { if ($Report.observations -cnotcontains $name) { throw "Missing scenario observation: $name" } }
    if (@($Report.observations | Select-Object -Unique).Count -ne @($Report.observations).Count) { throw 'Duplicate observations.' }
    $metricFields=@('tickCount','renderCount','allocatedBytes','peakManagedBytes','peakUnityBytes','peakWorkingSetBytes','modelMeanMs','modelP95Ms','modelMaxMs','viewMeanMs','viewP95Ms','frameWallP95Ms','wallSeconds')
    Assert-EvidenceFields $Report.metrics ($metricFields + @('allocationSource','memorySource'))
    if ([string]::IsNullOrWhiteSpace($Report.metrics.allocationSource) -or [string]::IsNullOrWhiteSpace($Report.metrics.memorySource) -or $Report.metrics.allocatedBytes -le 0 -or $Report.metrics.peakWorkingSetBytes -le 0) { throw 'Allocation or process-memory evidence is unavailable.' }
    foreach ($field in $metricFields) { Assert-EvidenceNumber $Report.metrics.$field $field }
    Assert-EvidenceNumber $Report.steps 'steps'; Assert-EvidenceNumber $Report.simulationSeconds 'simulationSeconds'
    if ($Report.metrics.tickCount -le 0 -or $Report.metrics.tickCount -ne $Report.steps -or $Report.metrics.renderCount -le 0 -or $Report.metrics.wallSeconds -le 0 -or [Math]::Abs($Report.simulationSeconds - $Report.steps * .02) -gt .001) { throw 'No complete fixed-step runtime measurement.' }
    if (@($Report.samples).Count -eq 0) { throw 'Missing runtime samples.' }
    $sampleNumbers=@('elapsed','playerHealth','maxAttackDispatchDelay','oldestPathWaitSeconds','kills','launches','spawnedNormal','spawnedAir','spawnedBoss','pendingSpawnCount','units','experience','attracting','visibleUnits','visibleExperience','visibleAttacks','retainedViews','projectiles','pathRequests','scopeCount')
    foreach ($sample in $Report.samples) {
        Assert-EvidenceFields $sample (@('runId','phase','brain','target','pendingStrikes')+$sampleNumbers)
        if ($sample.runId -cnotmatch '^[a-f0-9]{32}$' -or $sample.pendingStrikes -cnotmatch '^[0-9]+$') { throw 'Missing sample identity or reservations.' }
        foreach ($field in $sampleNumbers) { Assert-EvidenceNumber $sample.$field $field }
    }
    $seconds=switch ($Scenario) { 'PersistentWorld10m' {600} 'PersistentWorld30m' {1800} 'CrowdCorridor' {12} default {0} }
    if ($Report.simulationSeconds + .00001 -lt $seconds) { throw 'Requested scenario duration was not executed.' }
    if ($Scenario -like 'PersistentWorld*' -and (@($Report.samples | Where-Object { $_.elapsed + .00001 -ge $seconds -and $_.spawnedBoss -ge ($seconds / 300) }).Count -eq 0)) { throw 'No final duration and boss observation.' }
}
function Assert-EvidenceFields {
    param($Value, [string[]]$Fields)
    if ($null -eq $Value) { throw 'Missing evidence object.' }
    foreach ($field in $Fields) { if ($Value.PSObject.Properties.Name -cnotcontains $field -or $null -eq $Value.$field) { throw "Missing evidence field: $field" } }
}
function Assert-EvidenceNumber {
    param($Value, [string]$Name)
    if ($null -eq $Value -or $Value -is [string] -or $Value -is [bool] -or [double]::IsNaN([double]$Value) -or [double]::IsInfinity([double]$Value) -or [double]$Value -lt 0) { throw "Invalid evidence number: $Name" }
}

function Read-ValidationJson {
    param([Parameter(Mandatory)][string]$Path, [switch]$IfAvailable)
    try {
        $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        try {
            $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8)
            try { return ($reader.ReadToEnd() | ConvertFrom-Json -DateKind String) } finally { $reader.Dispose() }
        } finally { $stream.Dispose() }
    } catch [IO.IOException] {
        if ($IfAvailable) { return $null }
        throw
    }
}

function Test-TestXml {
    param([xml]$Result, [AllowEmptyCollection()][string[]]$ExpectedNames)
    try {
        if ($null -eq $Result -or @($ExpectedNames).Count -eq 0) { return $false }
        $root = $Result.DocumentElement
        if ($root.Name -ne 'test-run' -or $root.GetAttribute('result') -ne 'Passed') { return $false }
        $cases = @($Result.SelectNodes('//test-case'))
        if ($cases.Count -eq 0 -or [int]$root.GetAttribute('total') -ne $cases.Count) { return $false }
        if (@($Result.SelectNodes('//failure | //error')).Count -gt 0) { return $false }
        foreach ($node in @($Result.SelectNodes('//test-suite | //test-case'))) {
            if ($node.GetAttribute('result') -ne 'Passed') { return $false }
        }
        foreach ($count in @('failed','skipped','inconclusive')) {
            if ($root.HasAttribute($count) -and [int]$root.GetAttribute($count) -ne 0) { return $false }
        }
        $names = @($cases | ForEach-Object { $_.GetAttribute('fullname') })
        if (@($names | Select-Object -Unique).Count -ne $names.Count) { return $false }
        if (@($ExpectedNames | Select-Object -Unique).Count -ne $ExpectedNames.Count) { return $false }
        if ($names.Count -ne $ExpectedNames.Count) { return $false }
        foreach ($name in $ExpectedNames) {
            if ([string]::IsNullOrWhiteSpace($name) -or $names -cnotcontains $name) { return $false }
        }
        return $true
    } catch { return $false }
}

function Assert-ValidationReceipt {
    param($Manifest, $Receipt)
    foreach ($field in @('schemaVersion','runId','sourceHash','mode','filter','unityVersion')) {
        if ($Manifest.$field -cne $Receipt.$field) { throw "Receipt mismatch: $field" }
    }
    if ($Receipt.schemaVersion -ne 1 -or $Receipt.runId -cnotmatch '^[a-f0-9]{32}$') { throw 'Invalid receipt identity.' }
    if ($Receipt.sourceHash -cnotmatch '^[a-f0-9]{64}$') { throw 'Invalid source hash.' }
    $mp=[IO.Path]::GetFullPath($Manifest.projectPath).TrimEnd('\','/')
    $rp=[IO.Path]::GetFullPath($Receipt.projectPath).TrimEnd('\','/')
    if (-not $mp.Equals($rp,[StringComparison]::OrdinalIgnoreCase)) { throw 'Receipt is from another project.' }
    if ($Receipt.status -cne 'Passed' -or $Receipt.errorCount -ne 0) { throw 'Validation did not pass without errors.' }
    if ($Receipt.requestedUtc -cne $Manifest.startedUtc) { throw 'Receipt is from another request time.' }
    $requested=[DateTimeOffset]::Parse($Manifest.startedUtc)
    $started=[DateTimeOffset]::Parse($Receipt.startedUtc)
    $completed=[DateTimeOffset]::Parse($Receipt.completedUtc)
    $runnerCompleted=[DateTimeOffset]::Parse($Manifest.completedUtc)
    if ($started -lt $requested -or $completed -lt $started -or $completed -gt $runnerCompleted) { throw 'Invalid receipt timestamps.' }
    if ($Receipt.mode -in @('EditMode','PlayMode')) {
        if (@($Receipt.discoveredNames).Count -eq 0) { throw 'No tests were discovered.' }
        if ($Receipt.xmlSha256 -cnotmatch '^[a-f0-9]{64}$') { throw 'Missing XML identity.' }
    }
    elseif ($Receipt.mode -in @('Smoke','Stress')) {
        if ($Receipt.scenario -cne $Manifest.scenario -or $Receipt.scenarioSha256 -cnotmatch '^[a-f0-9]{64}$') { throw 'Missing scenario identity.' }
    }
}

function Read-ValidationResult {
    param([Parameter(Mandatory)][string]$RunDirectory, [Parameter(Mandatory)][string]$CurrentSourceHash)
    $manifest=Read-ValidationJson (Join-Path $RunDirectory 'manifest.json')
    $receipt=Read-ValidationJson (Join-Path $RunDirectory 'receipt.json')
    Assert-ValidationReceipt $manifest $receipt
    if ($manifest.sourceHash -cne $CurrentSourceHash) { throw 'Sources changed after validation started.' }
    if ($manifest.mode -in @('EditMode','PlayMode')) {
        $xmlPath=Join-Path $RunDirectory ($manifest.mode.ToLowerInvariant()+'.xml')
        if ((Get-FileHash -LiteralPath $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $receipt.xmlSha256) { throw 'XML does not match this receipt.' }
        $settings=[Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit
        $reader=[Xml.XmlReader]::Create($xmlPath,$settings)
        try { $xml=[xml]::new(); $xml.Load($reader) } finally { $reader.Dispose() }
        if (-not (Test-TestXml $xml @($receipt.discoveredNames))) { throw 'Tests failed, were skipped, missing, duplicated, or not executed.' }
    }
    if ($manifest.mode -in @('Smoke','Stress')) {
        $path=Join-Path $RunDirectory 'scenario.json'
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $receipt.scenarioSha256) { throw 'Scenario does not match its receipt.' }
        $report=Read-ValidationJson $path
        Assert-ScenarioReport $report $manifest.runId $manifest.sourceHash $manifest.scenario
        if ($report.unityVersion -cne $manifest.unityVersion -or [DateTimeOffset]::Parse($report.startedUtc) -lt [DateTimeOffset]::Parse($manifest.startedUtc) -or [DateTimeOffset]::Parse($report.completedUtc) -gt [DateTimeOffset]::Parse($receipt.completedUtc)) { throw 'Runtime version or time mismatch.' }
    }
    return $receipt
}
