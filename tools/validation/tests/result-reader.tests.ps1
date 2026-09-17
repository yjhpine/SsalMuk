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
[IO.File]::WriteAllText($fixture,'{"status":"Ready","startedUtc":"2026-09-16T18:21:00.4534543Z"}')
try {
    Check 'Available JSON is read' { Must ((Read-ValidationJson $fixture).status -eq 'Ready') }
    Check 'Timestamp strings retain UTC and subsecond identity' {
        $timestamp=(Read-ValidationJson $fixture).startedUtc
        Must ($timestamp -is [string])
        Must ($timestamp -ceq '2026-09-16T18:21:00.4534543Z')
        Must ([DateTimeOffset]::Parse($timestamp).Offset -eq [TimeSpan]::Zero)
    }
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

function ScenarioFixture {
    [pscustomobject]@{
        schemaVersion=1;runId='0123456789abcdef0123456789abcdef';sourceHash=('a'*64);scenario='EndToEnd';status='Passed';errorCount=0
        startedUtc='2026-09-16T00:00:01Z';completedUtc='2026-09-16T00:00:02Z';unityVersion='6000.4.6f1';environment='Editor'
        processor='CPU';graphics='GPU';operatingSystem='Windows';systemMemoryMB=16384;width=1920;height=1080;seed=1;preset='Fixture';executionMode='Fixed ticks'
        steps=50;simulationSeconds=1
        observations=@('GameStart','Attack','ExperienceGrounded','ExperienceAttracting','ExperienceFlight','ExperienceContact','ChoiceButton','Death','Results','Restart','CleanScope')
        samples=@([pscustomobject]@{runId='11111111111111111111111111111111';phase='Running';brain='Collect';target='';pendingStrikes='1';elapsed=1;playerHealth=100;maxAttackDispatchDelay=.02;oldestPathWaitSeconds=0;kills=0;launches=1;spawnedNormal=0;spawnedAir=0;spawnedBoss=0;pendingSpawnCount=0;units=13;experience=0;attracting=0;visibleUnits=13;visibleExperience=0;visibleAttacks=0;retainedViews=13;projectiles=0;pathRequests=0;scopeCount=1})
        metrics=[pscustomobject]@{tickCount=50;renderCount=1;allocatedBytes=10;peakManagedBytes=100;peakUnityBytes=200;peakWorkingSetBytes=300;modelMeanMs=1;modelP95Ms=2;modelMaxMs=3;viewMeanMs=1;viewP95Ms=1;frameWallP95Ms=16;wallSeconds=1;allocationSource='Counter';memorySource='Counter'}
    }
}
function VerifyScenario($value) { Assert-ScenarioReport $value '0123456789abcdef0123456789abcdef' ('a'*64) 'EndToEnd' }
Check 'Complete runtime evidence is accepted' { VerifyScenario (ScenarioFixture) }
Check 'Scenario missing observation is rejected' { $r=ScenarioFixture; $r.observations=@('GameStart'); MustThrow { VerifyScenario $r } }
Check 'Missing metrics does not pass' { $r=ScenarioFixture; $r.PSObject.Properties.Remove('metrics'); MustThrow { VerifyScenario $r } }
Check 'Missing sample field does not become zero' { $r=ScenarioFixture; $r.samples[0].PSObject.Properties.Remove('pathRequests'); MustThrow { VerifyScenario $r } }
Check 'Other runtime identity is rejected' { $r=ScenarioFixture; $r.runId='old'; MustThrow { VerifyScenario $r } }
Check 'Different runtime source is rejected' { $r=ScenarioFixture; $r.sourceHash=('b'*64); MustThrow { VerifyScenario $r } }
Check 'No actual simulation ticks is rejected' { $r=ScenarioFixture; $r.metrics.tickCount=0; MustThrow { VerifyScenario $r } }
Check 'NaN model time is rejected' { $r=ScenarioFixture; $r.metrics.modelP95Ms=[double]::NaN; MustThrow { VerifyScenario $r } }
Check 'Runtime error is rejected' { $r=ScenarioFixture; $r.errorCount=1; MustThrow { VerifyScenario $r } }
Check 'Duplicate observations are rejected' { $r=ScenarioFixture; $r.observations+=@('GameStart'); MustThrow { VerifyScenario $r } }
Write-Output "Extended result reader: $script:passed passed."
