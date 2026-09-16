$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../read-results.ps1')

$script:passed = 0
function Check([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Output "PASS $Name"
}
function Must([bool]$Condition) { if (-not $Condition) { throw 'Assertion failed.' } }
function MustThrow([scriptblock]$Body) {
    $thrown = $false
    try { & $Body | Out-Null } catch { $thrown = $true }
    Must $thrown
}
function XmlFixture([string]$Result='Passed', [string]$Case='Passed', [string]$Name='Required') {
    [xml]"<test-run result='$Result' total='1' passed='1' failed='0' skipped='0' inconclusive='0'><test-suite result='$Result'><test-case fullname='$Name' result='$Case'/></test-suite></test-run>"
}

Check 'A real named passing test is accepted' { Must (Test-TestXml (XmlFixture) @('Required')) }
Check 'Zero tests do not pass' { Must (-not (Test-TestXml ([xml]'<test-run result="Passed" total="0"/>') @('Required'))) }
Check 'Missing expected test does not pass' { Must (-not (Test-TestXml (XmlFixture) @('Absent'))) }
Check 'Skipped is not success' { Must (-not (Test-TestXml (XmlFixture -Case 'Skipped') @('Required'))) }
Check 'Failure in a child overrides passing root' { Must (-not (Test-TestXml (XmlFixture -Case 'Failed') @('Required'))) }
Check 'Failed root is rejected' { Must (-not (Test-TestXml (XmlFixture -Result 'Failed') @('Required'))) }
Check 'Inconclusive is rejected' { Must (-not (Test-TestXml (XmlFixture -Case 'Inconclusive') @('Required'))) }
Check 'Empty discovery is rejected' { Must (-not (Test-TestXml (XmlFixture) @())) }
Check 'Mismatched declared count is rejected' {
    $x=XmlFixture; $x.'test-run'.SetAttribute('total','2'); Must (-not (Test-TestXml $x @('Required')))
}
Check 'Duplicate test results are rejected' {
    $x=XmlFixture; $x.'test-run'.SetAttribute('total','2')
    [void]$x.'test-run'.AppendChild($x.SelectSingleNode('//test-case').CloneNode($true))
    Must (-not (Test-TestXml $x @('Required')))
}
Check 'Nested setup failure is rejected' {
    $x=XmlFixture; $x.'test-run'.'test-suite'.SetAttribute('result','Failed'); Must (-not (Test-TestXml $x @('Required')))
}

function ManifestFixture {
    [pscustomobject]@{schemaVersion=1;runId='0123456789abcdef0123456789abcdef';sourceHash=('a'*64);projectPath='C:/Game';mode='EditMode';filter='SsalMuk.Tests';unityVersion='6000.4.6f1';startedUtc='2026-09-16T00:00:00.0000000Z';completedUtc='2026-09-16T00:00:03.0000000Z'}
}
function ReceiptFixture {
    [pscustomobject]@{schemaVersion=1;runId='0123456789abcdef0123456789abcdef';sourceHash=('a'*64);projectPath='C:/Game';mode='EditMode';filter='SsalMuk.Tests';requestedUtc='2026-09-16T00:00:00.0000000Z';startedUtc='2026-09-16T00:00:01.0000000Z';completedUtc='2026-09-16T00:00:02.0000000Z';status='Passed';errorCount=0;discoveredNames=@('Required');xmlSha256=('b'*64);unityVersion='6000.4.6f1'}
}
Check 'Current receipt is accepted' { Assert-ValidationReceipt (ManifestFixture) (ReceiptFixture) }
Check 'Old run id is rejected' { $r=ReceiptFixture; $r.runId='old'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Other source hash is rejected' { $r=ReceiptFixture; $r.sourceHash='changed'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Other project is rejected' { $r=ReceiptFixture; $r.projectPath='C:/Other'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Other mode is rejected' { $r=ReceiptFixture; $r.mode='PlayMode'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Other Unity version is rejected' { $r=ReceiptFixture; $r.unityVersion='6000.3.0f1'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Other filter is rejected' { $r=ReceiptFixture; $r.filter='Other'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Receipt predating request is rejected' { $r=ReceiptFixture; $r.startedUtc='2026-09-15T23:59:59Z'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Reversed timestamps are rejected' { $r=ReceiptFixture; $r.completedUtc='2026-09-15T23:59:59Z'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Receipt from after runner completion is rejected' { $r=ReceiptFixture; $r.completedUtc='2026-09-16T00:00:04Z'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Error count is rejected' { $r=ReceiptFixture; $r.errorCount=1; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Failed status is rejected' { $r=ReceiptFixture; $r.status='Failed'; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'No discovered tests is rejected' { $r=ReceiptFixture; $r.discoveredNames=@(); MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
Check 'Missing XML identity is rejected' { $r=ReceiptFixture; $r.xmlSha256=''; MustThrow { Assert-ValidationReceipt (ManifestFixture) $r } }
$fixture=Join-Path $PSScriptRoot ('../../../Logs/Validation/reader-fixture-'+[guid]::NewGuid().ToString('N')+'.json')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture))) | Out-Null
[IO.File]::WriteAllText($fixture,'{"status":"Ready"}')
try {
    Check 'Available JSON is read' { Must ((Read-ValidationJson $fixture).status -eq 'Ready') }
    Check 'Optional heartbeat retries an exclusively locked file' {
        $lock=[IO.File]::Open($fixture,'Open','ReadWrite','None')
        try { Must ($null -eq (Read-ValidationJson $fixture -IfAvailable)) }
        finally { $lock.Dispose() }
    }
    Check 'Required evidence does not silently ignore a locked file' {
        $lock=[IO.File]::Open($fixture,'Open','ReadWrite','None')
        try { MustThrow { Read-ValidationJson $fixture } }
        finally { $lock.Dispose() }
    }
} finally { [IO.File]::Delete($fixture) }
Write-Output "Result reader: $script:passed passed."
