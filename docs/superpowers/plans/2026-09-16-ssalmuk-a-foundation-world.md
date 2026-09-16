# SsalMuk A: Foundation and World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Unity 연결·검증 경로를 구축하고, 실행 중 생성된 지상 몬스터가 장애물을 돌아 군집을 이루는 장면을 만든다.

**Architecture:** C# 모델을 먼저 검증하고 Unity가 그 결과를 표시한다. 청크 지형·공간 검색·군집 이동을 분리하며 게임 개체는 실행 시 생성한다.

**Tech Stack:** Unity `6000.4.6f1`, C#, Test Framework `1.6.0`, uGUI, PowerShell, MCP for Unity `10.2.0`.

**Spec:** [구조 설계](../specs/2026-09-16-ssalmuk-architecture-design.md) 2~7·13절, [전체 계획](2026-09-16-ssalmuk-implementation-plan.md).

## Global Constraints

- 전체 계획의 Global Constraints와 공통 타입·검증 명령을 모두 적용한다.
- `C:/Users/ace21/Desktop/SsalMuk`에서 작업하며 Unity `6000.4.6f1`을 유지한다.
- 기존 사용자 설정 변경을 보존한다. 월드·유닛을 씬에 미리 배치하지 않는다.
- 경험치와 모든 몬스터를 계속 유지한다. 여기서는 지형 캐시와 표시만 회수할 수 있다.
- 본 단계는 이동 장면까지이며 전투·성장 완료로 보고하지 않는다.

---

## Task A1: 대상 Editor 연결과 신뢰할 수 있는 검증 실행

**Files:** 수정 `Packages/manifest.json`, `Packages/packages-lock.json`; 생성 `AGENTS.md`, `tools/validation/validate.ps1`, `tools/validation/read-results.ps1`, `tools/validation/tests/result-reader.tests.ps1`, `Assets/_SsalMuk/Editor/Validation/EditorValidationBridge.cs`, `ValidationCallbacks.cs`, `ValidationRequest.cs`, `Assets/_SsalMuk/Editor/SsalMuk.Editor.asmdef`, `Assets/_SsalMuk/Tests/EditMode/ValidationBridgeTests.cs`, `SsalMuk.Tests.EditMode.asmdef`, `Assets/_SsalMuk/Tests/PlayMode/SsalMuk.Tests.PlayMode.asmdef`.

**Interfaces:** `validate.ps1 -Mode Static|EditMode|PlayMode|Smoke|Stress -Filter <정규 테스트 이름> -Scenario <시나리오 이름>`; 결과는 실행 ID별 manifest/XML/receipt. `Test-TestXml([xml]$Result, [string[]]$ExpectedNames)`는 필수 테스트가 모두 성공했을 때만 true. `Assert-ValidationReceipt($Manifest,$Receipt)`는 ID·소스 해시·시각 불일치 시 예외.

- [x] 열린 Editor의 프로젝트 경로와 버전을 필요한 필드만 읽어 확인한다. 프로세스 명령행·전역 설정 전체는 인증값을 포함할 수 있으므로 출력하지 않는다. 기존 dirty 파일의 시작 상태와 패키지 목록을 `Logs/Validation/` 아래에 보관한다.
- [x] 아래 고정 버전의 Unity 패키지 항목만 추가하고 import를 확인한다. Test Framework의 프로젝트 직접 의존성 `1.6.0`을 유지하고 다른 패키지의 의도치 않은 변경 여부를 비교한다.

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.2.0"
```

- [x] 공식 서버도 `mcpforunityserver==10.2.0`으로 맞춘다. 기존 HTTP 주소와 실행 중 서버를 먼저 검사하고, 서버가 없을 때만 loopback에 실행한다. Windows의 별도 실행 프로세스는 숨김으로 시작한다. 기존 Codex `unityMCP` 설정을 재사용하고 다른 MCP 항목을 덮어쓰지 않는다. Unity 프로젝트·버전 읽기까지 성공한 증거를 남긴다. 설치 경로와 서버 버전은 [공식 설치 방식](https://github.com/CoplayDev/unity-mcp/blob/main/README.md)과 [서버 정의](https://github.com/CoplayDev/unity-mcp/blob/v10.2.0/Server/pyproject.toml)에 근거한다.
- [x] 이 단계의 Editor asmdef는 TestRunner API만 참조하고 아직 없는 게임 조립 단위는 참조하지 않는다. 테스트 asmdef의 이름은 파일명과 일치시킨다. 결과 판독기의 실패 테스트를 작성하고 아래 입력을 거절하는지 확인한다. 아직 함수가 없을 때는 함수 미정의로 실패해야 한다.

```powershell
. ./tools/validation/read-results.ps1
$empty = [xml]'<test-run result="Passed" total="0" passed="0" failed="0" />'
if (Test-TestXml $empty @('SsalMuk.Tests.ValidationBridgeTests.RoundTrip')) {
    throw 'A zero-test run must not pass.'
}
$skipped = [xml]'<test-run result="Passed"><test-case fullname="Required" result="Skipped" /></test-run>'
if (Test-TestXml $skipped @('Required')) { throw 'Skipped is not passed.' }
```

- [x] `read-results.ps1`에서 testcase를 재귀 조회해 필수 이름 존재·Passed·오류 없음·실행 수 양수를 확인한다. manifest/receipt의 실행 ID·시작/종료 시각·소스 해시를 별도로 대조한다. 종료 코드만으로 성공하지 않는다. 정상 XML, 실패 XML, 지난 실행 ID, 다른 소스 해시, 필수 테스트 누락의 테스트를 함께 추가한다.
- [x] `validate.ps1`에 매번 새 실행 폴더를 만드는 로직과 경로 검증을 작성한다. 열린 Editor에는 `request.json`을 임시 파일 작성 후 이름 변경으로 전달한다. 닫힌 프로젝트에만 배치 실행을 사용한다. 제한 시간 초과·컴파일 실패·실행 실패를 각각 기록하고 종료 코드를 실패로 반환한다.
- [x] Editor 연결은 정해진 프로젝트의 요청 폴더만 읽는다. 허용된 모드·테스트 필터·시나리오만 처리하고, 실행 중 요청은 중복 수락하지 않는다. TestRunner API 진입은 다음 형태로 작성한다. `ValidationCallbacks`가 EditorValidationBridge에 완료를 전달한다. XML을 먼저 저장하고 Test Runner의 씬·설정 복원 뒤 receipt를 마지막에 기록한다.

```csharp
var api = ScriptableObject.CreateInstance<TestRunnerApi>();
TestRunnerApi.RegisterTestCallback(new ValidationCallbacks());
string[] groups = string.IsNullOrEmpty(filter) ? null : new[] {
    "^" + System.Text.RegularExpressions.Regex.Escape(filter) + @"(?:\.|$)"
};
api.Execute(new ExecutionSettings(new Filter {
    testMode = mode,
    groupNames = groups,
    assemblyNames = mode == TestMode.EditMode
        ? new[] { "SsalMuk.Tests.EditMode" }
        : new[] { "SsalMuk.Tests.PlayMode" }
}));
```

- [x] `ValidationCallbacks`는 설치된 `ICallbacks`의 RunStarted/RunFinished/TestStarted/TestFinished를 구현한다. `TestRunnerApi.SaveResultToFile`을 사용한다. 활성 요청 ID는 Editor SessionState에 보존해 Domain Reload 뒤 완료 콜백을 다시 연결하고 요청 재실행을 막는다. 스크립트의 정상·실패 fixture와 실제 테스트 1개 실행으로 파일 왕복을 검증한다.
- [x] `AGENTS.md`에 기준 문서, 작업 경로, 승인된 범위 내 자율 진행, 사용자 변경 보존, 새 결과 판독, 작업 단위 로컬 커밋, 짧은 보고·log.md 규칙을 기록한다. 연결 성공과 검증 왕복 성공을 각각 확인한 뒤 `chore: connect Unity and add validation harness`로 해당 파일만 커밋한다.

## Task A2: 조립 단위·공유 정의·유닛 생성·시계

**Files:** 생성 `Core/SsalMuk.Core.asmdef`, `Presentation/SsalMuk.Presentation.asmdef`, `Unity/SsalMuk.Unity.asmdef`, `Core/Math/DVec2.cs`, `Core/World/ChunkCoord.cs`, `WorldPosition.cs`, `Core/Session/RunClock.cs`, `Core/Combat/WeaponKind.cs`, `Core/Progression/WeaponInventory.cs`, `WeaponState.cs`, `Core/Units/UnitKind.cs`, `UnitDefinition.cs`, `DefinitionCatalog.cs`, `UnitModel.cs`, `PlayerModel.cs`, `GroundEnemyModel.cs`, `AirEnemyModel.cs`, `UnitFactory.cs`, `PlayerFactory.cs`, `GroundEnemyFactory.cs`, `AirEnemyFactory.cs`, `UnitRegistry.cs`; 생성 `Unity/Configuration/GameCatalog.cs`, `DevelopmentDefaults.cs`, `Tests/EditMode/UnitFactoryTests.cs`, `RunClockTests.cs`. 경로 생략 파일은 직전 같은 디렉터리다.

**Interfaces:** `RunClock(double fixedStep)`의 `Start()`, `Stop()`, `Advance(int ticks = 1)`, `double ElapsedSeconds`; `DefinitionCatalog.GetUnit(UnitKind)`; `UnitFactory.Spawn(UnitSpawnRequest)` → `UnitModel`; `UnitSpawnRequest`는 UnitKind·WorldPosition·생성 정의와 RunId를 가진 값 객체로 `Core/Units/UnitSpawnRequest.cs`에 정의한다. WorldPosition은 크기 32인 청크 정규화·Offset·DistanceTo를 제공한다. UnitRegistry는 ID별 모델 등록·읽기·제거를 담당하고 A3 WorldStore가 이를 소유한다. PlayerModel의 `Weapons.Kinds`는 초기 검만 포함하며 `Level`은 BigInteger.One으로 시작한다. WeaponState는 무기 종류를 보관하고 강화 상태는 C1에서 추가한다.

- [ ] Core·Presentation은 `noEngineReferences: true`, Unity는 두 조립 단위를 참조하도록 구성한다. A1의 Editor·테스트 asmdef에 이제 생성된 게임 조립 단위 참조를 추가한다. 테스트 asmdef는 TestAssemblies 표시가 주입하는 NUnit/TestRunner 참조를 중복 선언하지 않는다. 각 테스트 fixture 필터가 실제 발견되는지 확인한다.
- [ ] 아래 시계 계약 테스트를 작성하고 예상 실패를 확인한다.

```csharp
[Test]
public void LoadingDoesNotConsumeSurvivalTime()
{
    var clock = new RunClock(0.02);
    clock.Advance(15000);
    Assert.That(clock.ElapsedSeconds, Is.Zero);
    clock.Start();
    clock.Advance(15000);
    Assert.That(clock.ElapsedSeconds, Is.EqualTo(300).Within(1e-9));
}
```

- [ ] 시계는 `running`일 때만 정수 tick을 증가시키고 `tick * fixedStep`으로 시간을 계산한다. 생성 정의에는 양수 체력·몸 반경·속도, 접촉 피해, BigInteger 경험치 보상을 둔다. `DVec2`는 영벡터 정규화 시 영벡터를 반환하고 NaN을 만들지 않게 한다.
- [ ] 읽기 전용 정의를 종류별 공유하고 유닛마다 현재 체력·위치·이동·피격 상태를 분리한다. 같은 정의로 서로 다른 위치에 두 적을 생성해 정의 참조는 같고 ID·개별 위치는 분리되는지 확인한다. 실제 피해 뒤 다른 개체와 정의의 체력이 변하지 않는 검증은 DamageService를 만드는 B2에 포함한다.
- [ ] Factory Method의 공통 검증·ID 발급·등록은 UnitFactory에 두고 `protected abstract UnitModel CreateUnit(UnitSpawnRequest request)`만 하위 팩토리가 재정의한다. Normal과 Boss는 GroundEnemyModel을 사용하고 스펙·반경만 다르게 한다. 무작위 분기 함수에 Factory Method라는 이름만 붙이지 않는다.
- [ ] `GameCatalog`가 정의와 표시 에셋을 참조하도록 만들고 중복 ID·누락 정의·잘못된 반경을 Editor와 실행 시작에서 검출한다. 정의 에셋에는 현재 체력·RunId를 직렬화하지 않는다. 관련 테스트를 통과시킨 뒤 `feat: add shared definitions and runtime unit factories`로 커밋한다.

## Task A3: 좌표·청크 생성·공간 검색·보존 저장소

**Files:** 수정 `Core/World/WorldPosition.cs`; 생성 `Core/World/ChunkData.cs`, `MapSettings.cs`, `ChunkGenerator.cs`, `WorldStore.cs`, `ExperienceRecord.cs`, `ExperienceState.cs`, `SpatialIndex.cs`, `IWorldQuery.cs`, `WorldQuery.cs`, `SweepHit.cs`, `Core/Movement/CircleSweep.cs`; 생성 `Core/Random/IRandomSource.cs`, `SeededRandom.cs`, `SeedStreams.cs`, `Tests/EditMode/WorldPositionTests.cs`, `WorldGenerationTests.cs`, `WorldQueryTests.cs`.

**Interfaces:** `WorldPosition.FromLocal(DVec2)`, `DistanceTo(WorldPosition)`, `Offset(DVec2)`; `ChunkGenerator(int seed, MapSettings settings).Generate(ChunkCoord)` → `ChunkData`; `MapSettings.TestDefaults(double obstacleChance = 0.2)`는 셀 1, 청크 한 변 32, 통로 최소 폭 4의 테스트 설정. `ChunkData.Fingerprint`는 동일 셀 결과의 안정 해시. `WorldStore`는 UnitRegistry와 ID별 ExperienceRecord를 보존한다. `IWorldQuery.FindNearestEnemy(WorldPosition)` → `long?`, `QueryCircle(WorldPosition,double)` → 대상 ID 목록, `SweepCircle(WorldPosition,DVec2,double)` → `SweepHit?`의 최초 구조물 충돌. SweepHit은 이동 비율·법선·충돌 위치를 담는다. `IRandomSource.NextUnit()` → double이며 `SeededRandom(int seed)`가 구현한다.

- [ ] 음수 좌표 정규화와 생성 순서 독립성 테스트를 작성하고 실패를 확인한다.

```csharp
[Test]
public void ChunkDoesNotDependOnVisitOrder()
{
    var generator = new ChunkGenerator(1234, MapSettings.TestDefaults());
    var coord = new ChunkCoord(-1, 2);
    var first = generator.Generate(coord).Fingerprint;
    generator.Generate(new ChunkCoord(50, -70));
    Assert.That(generator.Generate(coord).Fingerprint, Is.EqualTo(first));
}
```

- [ ] WorldPosition은 청크와 내부 좌표를 정규화해 저장한다. 몫은 floor로 계산하고 차이를 구할 때 가까운 청크 간 상대 좌표부터 계산한다. 전체 큰 좌표를 먼저 float로 변환하지 않는다. 테스트는 -0.1, -32, 32, 먼 청크 간 작은 상대 이동을 포함한다.
- [ ] 청크 시드는 실행 시드·정수 좌표·생성 버전을 명시적 안정 해시로 조합한다. 프로세스마다 달라질 수 있는 string.GetHashCode나 UnityEngine.Random을 사용하지 않는다. 공유 경계는 방향·경계의 전역 정수 좌표·시드로 출입구 위치를 정해 양쪽 청크가 같은 값을 얻도록 한다. 출입구들을 중앙 연결 구역으로 잇고 장애물을 배치한 뒤 바닥 연결·몸 반경 여유를 flood fill로 검사한다. 실패하면 같은 출입구의 안전 통로 배치를 반환한다.
- [ ] SeededRandom은 아래 uint 상태 전이를 사용한다. 생성자에서 seed를 uint로 변환하고 0이면 `0x6D2B79F5u`로 초기화한다. NextUnit은 반환값을 `4294967296.0`으로 나누어 0 이상 1 미만으로 만든다. SeedStreams는 Map/Spawn/Reward별 고정 태그로 초기 시드를 분리한다. 같은 시드·호출 수의 재현과 보상 난수 소비가 지형을 바꾸지 않는 테스트를 추가한다.

```csharp
uint NextUInt()
{
    uint x = state;
    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;
    state = x;
    return x;
}
```
- [ ] 청크 경계의 출입구와 큰 보스 통과 폭을 여러 양수·음수 좌표에서 검증한다. 단일 청크뿐 아니라 인접 청크를 합친 연결성도 검사한다.
- [ ] SpatialIndex는 셀별 ID 목록을 관리하고 이동 시 이전 셀에서 제거 후 새 셀에 등록한다. 최근접은 청크 밖 후보까지 확장하며 다음 영역의 최소 거리가 현재 최선보다 멀 때만 종료한다. 대상이 없는 경우 유한한 비어 있지 않은 청크 색인으로 검색을 끝낸다.
- [ ] CircleSweep에 원의 이동 구간과 구조물 셀의 최초 접촉을 구현해 WorldQuery.SweepCircle에 연결한다. 이동 구간이 닿는 셀만 후보로 삼고 셀의 변과 모서리에 대한 최초 접촉 비율·법선을 반환한다. 이 단계에서는 위치를 이동시키지 않고 기하 결과만 반환한다.
- [ ] ExperienceRecord는 `Id`, `Position`(WorldPosition), `Value`(BigInteger), `State`(ExperienceState)를 읽기 전용으로 노출하며 초기 상태는 Grounded다. ExperienceState는 Grounded/Attracting/Collected를 정의한다. `WorldStore.TryGetExperience(long id,out ExperienceRecord record)`로 조회하고, 상태·위치 변경과 색인 갱신은 월드가 소유한다. 논리 경험치는 실제 접촉 획득 또는 판 종료 때만 삭제한다. 몬스터 사망은 삭제가 아닌 드롭 생성 원인이다. 정적 청크 캐시 회수 뒤 동일 지형 재생성과 몬스터·경험치 ID·값·상태 유지 테스트를 작성한다. 흡수 이동·지급은 C1에서 연결한다. A2 포함 관련 테스트를 통과시킨 뒤 `feat: add deterministic chunks and persistent world data`로 커밋한다.

## Task A4: 길찾기와 몬스터 밀집 이동

**Files:** 생성 `Core/Navigation/INavigation.cs`, `GridAStar.cs`, `FlowField.cs`, `ChunkRoutePlanner.cs`, `NavigationService.cs`; 수정 `Core/Movement/CircleSweep.cs`; 생성 `Core/Movement/CrowdSolver.cs`, `MovementSystem.cs`; 생성 `Tests/Shared/SsalMuk.Tests.Shared.asmdef`, `RunTestRig.cs`, `Tests/EditMode/NavigationTests.cs`, `CrowdMovementTests.cs`.

**Interfaces:** `INavigation.RequestPath(WorldPosition from, WorldPosition to, double radius)` → `PathRequest`; `PathRequest`는 `Status(Pending|Ready|NoPath)`, 읽기 전용 Waypoints를 가진 `Core/Navigation/PathRequest.cs`. `CrowdSolver.Resolve(WorldStore world, double dt)`와 `MovementSystem.Step(double dt)`는 논리 위치·색인을 갱신한다. RunTestRig의 A4 API는 전체 계획 표대로 구현한다. UnitModel에는 `Position`, `BodyRadius`, `Health`, `IsAlive` 읽기 속성을 제공한다.

- [ ] 모서리 관통, 통로 반경, 가까운 두 몬스터의 겹침 테스트를 먼저 작성한다.

```csharp
[Test]
public void GroundBodiesSeparateWithoutDeletingUnits()
{
    using var rig = RunTestRig.Create();
    rig.PlacePlayer(new DVec2(8, 0));
    long a = rig.Spawn(UnitKind.Normal, new DVec2(0, 0));
    long b = rig.Spawn(UnitKind.Normal, new DVec2(0.1, 0));
    rig.Advance(0.2);
    double distance = rig.Unit(a).Position.DistanceTo(rig.Unit(b).Position);
    Assert.That(distance, Is.GreaterThanOrEqualTo(
        rig.Unit(a).BodyRadius + rig.Unit(b).BodyRadius - 0.05));
    Assert.That(rig.Unit(a).IsAlive && rig.Unit(b).IsAlive, Is.True);
}
```

- [ ] A*는 반경만큼 확장한 장애물을 사용하고 대각선 코너 잘림을 금지한다. 플레이어용 비용은 거리+음수가 아닌 위험 비용이다. 예산 내 미완료 요청은 Pending을 유지하며 직전 유효 경로만 사용한다. 도달 불가를 직선 벽 통과로 대체하지 않는다.
- [ ] 일반·보스의 반경 등급별로 플레이어 목표 셀에서 역방향 거리장을 만든다. 목표 셀·관련 청크 버전 변경 시 갱신한다. 원거리는 청크 출입구 그래프 A*와 청크 내부 길로 연결한다. 경로가 캐시 회수로 사라지면 데이터 재생성 뒤 이어간다.
- [ ] CircleSweep에서 이동 구간을 검사하고 충돌면을 향하는 성분을 제거해 접선 이동을 남긴다. 시작부터 벽과 겹친 입력은 안전 위치 검증 오류로 처리하고, 정상 이동·넉백·군집 보정 뒤에는 구조물 침범을 재검사한다.
- [ ] CrowdSolver는 근처 지상 쌍만 ID 순서로 처리한다. 겹침량 `max(0, radiusA + radiusB - distance)`를 나누어 보정한다. 완전히 같은 중심은 두 ID로 정한 안정된 분리 방향을 사용한다. 접촉면의 안쪽 이동을 제거하고 옆 공간이 있을 때 접선 방향으로 진행한다. 정면 접촉으로 접선 성분도 0이면 양옆 여유를 검사해 한쪽을 유지하고, 동률은 ID로 결정한다. 반발 계수는 0이며 보행 스프라이트 크기로 몸 반경을 바꾸지 않는다.
- [ ] 군집 반복 보정 횟수를 설정으로 두고 통로 막힘·벽 옆 압축·공중 통과·넉백 밀집 테스트를 추가한다. 분리 반경은 몸이 맞닿는 수준으로 두고 앞이 완전히 막히면 정체를 허용한다. 먼 지상 개체도 이동 계산에서 누락하지 않는다.
- [ ] `RunTestRig.Advance`를 실제 MovementSystem에 연결하고 공간 검색·청크 경계 테스트를 함께 확인한다. 통과 후 `feat: add navigation and sliding enemy crowds`로 커밋한다.

## Task A5: 런타임 생성 장면과 MVP 표시

**Files:** 생성 `Core/Session/RunPhase.cs`, `RunModel.cs`, `RunCoordinator.cs`, `IRunBuilder.cs`, `ISceneLoader.cs`; 생성 `Unity/Bootstrap/AppRoot.cs`, `RunScope.cs`, `UnitySceneLoader.cs`, `InitialRunBuilder.cs`, `BattleRunner.cs`; 생성 `Presentation/MainMenuPresenter.cs`, `WorldPresenter.cs`, `ViewContracts/IMainMenuView.cs`, `IWorldView.cs`; 생성 `Unity/Views/MainMenuView.cs`, `WorldView.cs`, `UnitView.cs`; 생성 `Editor/Content/DevelopmentContentBuilder.cs`, `Tests/PlayMode/SessionLifecycleTests.cs`; 생성 `Scenes/MainMenu.unity`, `Scenes/Battle.unity`, `Resources/Bootstrap/GameCatalog.asset`, `Content/Definitions/DevelopmentDefaults.asset`, `Content/Prefabs/UnitView.prefab`. 필요한 씬 목록만 `ProjectSettings/EditorBuildSettings.asset`에 추가한다.

**Interfaces:** `RunCoordinator.StartRunAsync()` → `Task`, `RunCoordinator.Phase`; `IRunBuilder : IDisposable`의 `BuildWorld()`, `CreatePlayer()`, `CreateInitialEnemies()`; `WorldPresenter.Refresh(IRunReadModel)`; `IRunReadModel`는 `Core/Session/IRunReadModel.cs`에 RunId·Phase·Clock·읽기 유닛/경험치 목록으로 정의한다. `ISceneLoader.LoadBattleAsync()`는 씬 완료 Task를 반환하고 UnitySceneLoader가 구현한다. RunModel은 `Id`, `Definitions`, `World`, `Clock`, `Player`와 처치 수를 소유한다. RunTestRig에 이 모델 조립과 `Run` 읽기를 추가한다.

- [ ] 시작 단계가 역전되거나 두 번 클릭에 판이 둘 생성되는 테스트를 작성한다. 지연 가능한 테스트 IRunBuilder로 각 단계를 관측하고, 실패 시 생성된 부분의 Dispose가 호출되는지도 검사한다.
- [ ] AppRoot는 시작 훅에서 하나만 만들고 새 RunScope를 조립한다. GameStart를 수락하면 Loading → BuildingWorld → CreatingPlayer → CreatingEnemies → Running을 순서대로 진행한다. 초기 생성 중 clock은 멈춰 둔다. 예외 시 해당 scope를 정리하고 메뉴로 복귀한다.

```csharp
await sceneLoader.LoadBattleAsync();
builder.BuildWorld();
builder.CreatePlayer();
builder.CreateInitialEnemies();
clock.Start();
```

- [ ] DevelopmentContentBuilder로 두 빈 씬·카탈로그·임시 단색 Sprite·UnitView 프리팹을 생성한다. 프리팹은 참조 에셋이며 씬 안에 개체를 넣지 않는다. 단색 이미지는 기능 확인용으로 표시하고 제공된 참고 이미지를 최종 게임 원화로 복사하지 않는다.
- [ ] Runtime에서 카메라·Canvas·EventSystem을 만들고 MVP 메뉴 버튼을 연결한다. 입력 모듈은 프로젝트의 Input System 설정과 맞추고 중복 EventSystem을 만들지 않는다. UI 글꼴 참조와 한글 표시를 확인한다. WorldPresenter는 모델 ID 기준으로 View를 갱신하고 현재 표시 기준점에 대한 상대 좌표만 Unity float에 전달한다.
- [ ] GameStart 뒤 구조물·플레이어·지상 몬스터가 자동 생성되고 몬스터가 장애물을 우회해 플레이어 주변에서 비비는 모습을 확인한다. 테스트 시나리오는 넓은 공간·좁은 통로·벽 옆 군집을 각각 사용한다. 본 단계의 플레이어는 정지 배치이며 자동 운영은 B1에서 연결된다.
- [ ] Scene 로드 실패·연속 클릭·직접 Battle 실행·Domain Reload 켜짐/꺼짐에서 생성 순서와 scope 수를 검증한다. 새 씬 항목만 스테이징하고 사용자 설정 변경을 보존한 채 `feat: bootstrap runtime scenes and crowd preview`로 커밋한다.

## 단계 A 완료

- [ ] A1~A5의 관련 테스트가 현재 소스에서 통과한다.
- [ ] 군집 이동 장면을 실제로 확인하고 과도한 튕김·벽 통과·공중 밀집 참여가 없다.
- [ ] 기존 사용자 변경이 보존되어 있고 다음 작업은 B1부터다.
