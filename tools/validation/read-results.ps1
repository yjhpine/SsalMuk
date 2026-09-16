Set-StrictMode -Version Latest

function Read-ValidationJson {
    param([Parameter(Mandatory)][string]$Path, [switch]$IfAvailable)
    try {
        $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        try {
            $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8)
            try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
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
    return $receipt
}
