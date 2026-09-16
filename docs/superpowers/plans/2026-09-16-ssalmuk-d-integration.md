# SsalMuk D: Endless Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 일반·공중·보스의 무한 출현, 피격·보행·공격 표현, 모든 개체 보존과 빌드 검증을 완성한다.

**Architecture:** SpawnDirector는 한 생존 시계의 독립 일정을 관리한다. 논리 개체는 WorldStore에 유지하고 WorldPresenter와 풀은 보이는 표현만 관리한다.

**Tech Stack:** Unity `6000.4.6f1`, C#, URP 2D, MVP, Unity Test Framework `1.6.0`, 개발 빌드 진단.

**Spec:** [구조 설계](../specs/2026-09-16-ssalmuk-architecture-design.md) 6·11~15절, [하네스](../../development/HARNESS.md), [아트·이펙트 기준](../../planning/ART_AND_VFX.md), [전체 계획](2026-09-16-ssalmuk-implementation-plan.md), [선행 C](2026-09-16-ssalmuk-c-growth-weapons.md).

## Global Constraints

- 전체 계획의 Global Constraints와 공통 타입·검증 명령을 모두 적용한다.
- 보스는 `300초`마다 이전 보스가 살아 있어도 추가 생성하며 고유 패턴은 없다.
- 경험치와 모든 몬스터를 계속 유지한다. 공중도 화면 이탈만으로 삭제하지 않는다.
- 피격·보행·공격 표시가 실제 몸 반경과 피해 판정을 바꾸지 않게 한다.
- 처리량이 부족하더라도 상한·개체 삭제·발동 누락을 몰래 넣지 않는다. 측정한 지원 범위와 미검증 범위를 구분한다.

---

## Task D1: 무한 스폰·공중 비행·보스 중첩

**Files:** 생성 `Core/Spawning/BossSchedule.cs`, `SpawnTicket.cs`, `SpawnDirector.cs`, `SpawnSettings.cs`, `DifficultyCurve.cs`, `AirGroupSpawner.cs`, `Core/World/WorldRect.cs`, `Core/AI/EnemyState.cs`, `EnemyFsm.cs`, `GroundChaseBehavior.cs`, `AirFlyBehavior.cs`, `KnockedBackBehavior.cs`, `DeadBehavior.cs`; 수정 `Core/Units/UnitSpawnRequest.cs`, `AirEnemyModel.cs`, `Core/Session/RunSimulation.cs`, `Unity/Bootstrap/InitialRunBuilder.cs`, `RunScope.cs`, `BattleRunner.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/SpawnScheduleTests.cs`, `AirMovementTests.cs`, `Tests/PlayMode/EnemyArchetypeTests.cs`.

**Interfaces:** `BossSchedule(double interval).CollectDueTimes(double now)` → IReadOnlyList<double>; `SpawnDirector.Tick(double now,WorldRect viewBounds)`; WorldRect는 WorldPosition 중심과 double 반너비·반높이를 가진다. BattleRunner가 카메라 사각형을 변환해 `RunSimulation.SetViewBounds(WorldRect)`로 전달하고 다음 주기에 사용한다. SpawnTicket은 RunId·종류·고유 일정 ID·예정 시각·미완료 생성 수를 가진다. `EnemyFsm.Tick(double dt)`는 `Chase/FlyThrough/Knockback/Dead`를 전환한다. AirEnemyModel은 `OriginalDirection`과 비행 속도를 보존한다. UnitSpawnRequest에 공중의 초기 방향을 추가한다.

- [x] 보스 경계·긴 갱신·재호출의 테스트를 작성하고 실패를 확인한다.

```csharp
[Test]
public void BossDeadlinesAreNeverSkippedOrRepeated()
{
    var schedule = new BossSchedule(300);
    Assert.That(schedule.CollectDueTimes(299.9), Is.Empty);
    Assert.That(schedule.CollectDueTimes(900), Is.EqualTo(new[] { 300d, 600d, 900d }));
    Assert.That(schedule.CollectDueTimes(900), Is.Empty);
}
```

- [x] 도래한 일정은 고유 ticket으로 발급한다. 생성 가능한 위치가 없으면 ticket을 남기고 재시도하며 새 보스 일정 진행과 분리한다. 위치 후보는 카메라 밖 바닥에서 검사하고 몸 반경·구조물·기존 지상 개체 겹침을 피한다. 같은 ticket의 성공을 두 번 기록하지 않는다.
- [x] 일반·공중·보스의 일정을 분리하고 RunClock의 Running 시간만 사용한다. DifficultyCurve는 생성 시점 체력·피해·출현량을 반환한다. 기존 적의 현재 체력을 난이도 상승 때 다시 계산하거나 회복시키지 않는다.
- [x] 공중 무리는 시드 난수로 화면 한쪽을 고른 뒤 출현 당시 플레이어 기준 방향 하나를 고정한다. 개체 시작점은 그 방향의 수직 축으로 벌리고 같은 방향으로 직진시킨다. 구조물·지상·공중끼리 밀집 보정에 참여시키지 않되 플레이어 이동과의 구간 접촉은 계산한다.
- [x] EnemyFsm을 A4 지상 추적에 연결하고 실제 피격의 넉백 시각 동안 KnockedBackBehavior를 실행한다. 공중은 종료 뒤 OriginalDirection으로 복귀한다. 보스는 GroundChaseBehavior에 큰 반경·높은 스펙을 사용하고 패턴 실행기를 추가하지 않는다.
- [x] 두 보스가 동시에 살아 있는 상태, 일반·공중·보스 동시 접촉, 공중의 구조물 관통·넉백 복귀·화면 밖 보존을 검증한다. 개체 보존 때문에 신규 스폰이 취소되지 않는지도 확인한다.
- [x] TestRig의 기본값은 명시적 배치만 사용해 다른 테스트에 스폰 난수가 섞이지 않게 한다. 일정 통합 테스트는 `RunTestRig.Create(scheduledSpawns: true)`로 실제 SpawnDirector를 연결한다. 실제 플레이에서는 항상 연결한다. 통과 후 `feat: add timed air waves and overlapping bosses`로 커밋한다.

## Task D2: AI 스프라이트·공격 에셋·경험치 발광과 풀

**Files:** 생성 `Core/Combat/AttackShapeSnapshot.cs`, `Unity/Views/WalkAnimator.cs`, `HitFeedback.cs`, `AttackView.cs`, `ProjectileView.cs`, `ExperienceView.cs`, `ViewPool.cs`, `LeaseToken.cs`, `WorldRenderOrigin.cs`; 수정 `Unity/Views/UnitView.cs`, `WorldView.cs`, `Presentation/WorldPresenter.cs`, `Core/Session/IRunReadModel.cs`, `Editor/Content/DevelopmentContentBuilder.cs`, `Content/Prefabs/UnitView.prefab`; 생성 `Tests/EditMode/VisualPoseTests.cs`, `Tests/PlayMode/FeedbackAndPoolTests.cs`, `PersistentWorldViewTests.cs`.

**Art files:** 생성 `Assets/_SsalMuk/Content/Art/Characters/`, `Monsters/`, `Effects/Experience/`, `ThirdParty/` 아래 실제 취득한 이미지와 `.meta`; 생성 `docs/planning/ASSET_SOURCES.md`에 실제 제작·취득 기록. 생성 `Unity/Configuration/VisualCatalog.cs`, `Content/Definitions/VisualCatalog.asset`, `Tests/PlayMode/ExperienceVisualTests.cs`; 수정 `Unity/Configuration/GameCatalog.cs`, `Editor/Content/DevelopmentContentBuilder.cs`와 기존 표시 참조. 아트 경로의 이후 상대 폴더는 `Assets/_SsalMuk/Content/Art/` 아래다.

**Interfaces:** `WalkPose.Sample(double phase,double angle,double height)` → `WalkPose`는 순수 계산용 타입으로 `Presentation/WalkPose.cs`에 작성하고 RotationDegrees·Height를 제공한다. `ViewPool.Rent(long entityId)` → LeaseToken, `Return(LeaseToken)`는 세대 일치 시만 회수한다. `WorldRenderOrigin.ToViewPosition(WorldPosition)`은 상대 좌표를 Unity Vector3로 변환한다. AttackShapeSnapshot은 모델 형상·진행률·발동 ID를 읽기 전용으로 전달한다.

**Visual interfaces:** VisualCatalog는 유닛 종류별 Sprite, 무기 종류별 Sprite 프레임·프레임 시간·기준 축/중심·원본 표시 크기, ExperienceTier별 색·halo 크기를 보유한다. WorldPresenter는 C1 PickupSettings에서 계산한 등급과 논리 위치·상태를 ExperienceView에 전달한다. `ExperienceView.Bind(long id,ExperienceTier tier)`, `SetPosition(Vector3)`, `ResetVisuals()`는 표시만 바꾸고 값을 지급하지 않는다.

- [ ] 보행의 네 지점과 입력 0의 정자세, 피격 표시의 복귀, 풀 세대 불일치 테스트를 작성한다.

- [ ] imagegen 스킬/도구로 플레이어·일반·공중·보스에 사용할 단일 스프라이트를 제작한다. 사용자 허용 범위에서 동일 화풍·투명 배경·시점·픽셀 밀도를 맞추고 무기/공격 효과를 본체에서 분리한다. 이미지 결과를 먼저 시각 확인하고 프롬프트·원본·적용 파일을 ASSET_SOURCES.md에 기록한다. 외형을 이유로 적 종류나 전투 규칙을 추가하지 않는다.
- [ ] 아트·이펙트 기준의 무료 우선 후보에서 필요한 원본을 취득하고 포함 라이선스·제작자·파일/버전·출처를 ASSET_SOURCES.md에 기록한다. 무료 샘플과 유료 전체 팩을 구분하고 유료 구매가 필요하면 구체적인 선택 결과를 제시한다. 취득한 프레임을 확인한 뒤 Unity Sprite로 import하고 투명도·축·중심·픽셀 밀도·재생 시간을 VisualCatalog에 설정한다. 공급자 페이지의 Unity 지원 문구만으로 적용 검증을 대신하지 않는다.

```csharp
[Test]
public void WalkingReturnsToTheFloorBetweenHops()
{
    Assert.That(WalkPose.Sample(0, 8, 0.08).Height, Is.Zero.Within(1e-8));
    Assert.That(WalkPose.Sample(Math.PI / 2, 8, 0.08).Height, Is.EqualTo(0.08).Within(1e-8));
    Assert.That(WalkPose.Sample(Math.PI, 8, 0.08).Height, Is.Zero.Within(1e-8));
    Assert.That(WalkPose.Sample(3 * Math.PI / 2, 8, 0.08).RotationDegrees, Is.LessThan(0));
}
```

- [ ] UnitView 계층을 `UnitViewRoot → WalkPivot → HitScaleRoot → Sprite`로 구성하고 Sprite의 바닥을 기준점으로 둔다. 걷기 입력이 있을 때 회전은 `angle*sin(phase)`, 상승은 `height*abs(sin(phase))`로 계산한다. 입력이 끊기면 짧게 정자세로 복귀시킨다. 공격·넉백 이벤트만으로 걷기 입력을 만들지 않는다.
- [ ] 실제 수락된 Damage 이벤트만 HitFeedback에 전달한다. 빨간색 1회 점멸 후 원래 색, 팽창 후 원래 scale로 복귀한다. 다시 맞으면 최신 이벤트 기준으로 연출을 갱신한다. 몸 반경·무기 기준점·그림자는 WalkPivot/HitScaleRoot의 자식으로 두지 않는다.
- [ ] 검·창·도끼·폭발은 AttackShapeSnapshot과 같은 형상을 반투명 Mesh/표시로 만들고, 취득한 공격 에셋의 방향·크기·프레임 진행을 같은 스냅샷에 맞춘다. 장식 잔상이 판정 범위를 가리지 않게 하고 에셋의 콜라이더/피해 콜백은 연결하지 않는다. 투사체·복사본·연속 공격은 발동 ID에 묶어 표시한다. 사거리 증가가 표시와 판정에 함께 적용되는 화면과 테스트를 확인한다.
- [ ] ExperienceView는 작은 원형 중심과 부드러운 halo를 사용한다. 초록/파랑/빨강 등급별 색을 적용하고 발광 느낌은 표시 재질과 겹친 halo로 먼저 만든다. halo 크기로 몸 접촉 반경을 늘리지 않는다. 필요에 따라 후처리를 조정하되 많은 구슬의 빛 번짐이 적·공격 범위를 가리는지 실제 화면에서 확인한다.
- [ ] 구슬의 비행 표시는 Core의 현재/이전 논리 위치를 보간한다. 반경 진입 직후 아직 경험치가 0인 화면, 몸 접촉 뒤 값이 증가하는 화면, 플레이어 이동 중 추적을 PlayMode에서 확인한다. 1/5/25 구슬을 함께 표시해 크기·발광·가치 순서를 확인하고, 빨강 구슬 View를 회수해 초록 구슬에 재대여했을 때 색·halo·위치가 초기화되는지 검사한다.
- [ ] 플레이어 발 위치 Y로 지상 스프라이트의 그리기 순서를 정해 앞뒤 겹침을 읽기 쉽게 한다. 점프·피격 scale은 그리기 순서 기준점을 바꾸지 않는다. 공중은 별도 표시 층을 사용하지만 피해 대상에서는 제외하지 않는다.
- [ ] View의 진입·이탈 거리를 다르게 두고 먼 표시만 회수한다. 논리 몬스터·경험치 ID·현재 위치·값·상태·체력은 WorldStore에 남긴다. 흡수 중 구슬도 View 회수와 무관하게 추적하고 접촉 때만 지급한다. 화면 밖 큰 범위 공격과 경험치 드롭도 동일 데이터에 적용한다.
- [ ] 풀 회수 시 구독·색·scale·회전·위상·이펙트·예약 콜백을 해제한다. RunId·EntityId·대여 세대를 검사해 예전 이벤트가 새 개체에 적용되지 않게 한다. 상대 원점을 이동시켜도 논리 좌표와 무기 궤적은 변경하지 않는다.
- [ ] 먼 청크 왕복, 피격 중 표시 회수·재생성, 공격/경험치 흡수 중 재시작, 이동하지 않는 자동 공격을 실제 화면과 데이터로 검증한다. 실제 생성 스프라이트·취득 효과를 참고 이미지의 느낌과 비교하고 단색 임시 그림을 최종 원화로 보고하지 않는다. FeedbackAndPoolTests·PersistentWorldViewTests·ExperienceVisualTests와 화면 확인이 통과한 뒤 `feat: add game art effects and glowing experience views`로 커밋한다.

## Task D3: 시나리오 실행·장시간 보존·빌드 검증

**Files:** 생성 `Unity/Diagnostics/ScenarioRunner.cs`, `ScenarioDefinition.cs`, `RuntimeProbe.cs`, `MetricsCollector.cs`, `BuildSmokeRunner.cs`, `Editor/Validation/BuildValidator.cs`, `Tests/PlayMode/EndToEndTests.cs`, `Tests/EditMode/PersistenceStressTests.cs`; 수정 `tools/validation/validate.ps1`, `Editor/Validation/EditorValidationBridge.cs`, `Tests/Shared/RunTestRig.cs`, 전체 계획의 완료 체크박스.

**Interfaces:** `ScenarioRunner.Run(string scenarioId,string outputDirectory)` → 비동기 완료 상태; `RuntimeProbe.Capture()` → RunId·시계·FSM·목표·논리 수·표시 수·예약 수의 스냅샷. `BuildValidator.BuildWindows(string outputPath)`는 BuildReport를 확인한다. `BuildSmokeRunner`는 개발 빌드의 명시적 시나리오 인수가 있을 때만 실행한다. 일반 게임 메뉴에 검증 도구를 노출하지 않는다.

- [ ] ScenarioDefinition에 `EndToEnd`, `CrowdCorridor`, `PersistentWorld10m`, `PersistentWorld30m`, `HighGrowth`, `RepeatedRestart`를 등록한다. 알 수 없는 이름은 실패로 반환한다. Unity 연결 실패·컴파일 실패·시나리오 실패를 다른 결과로 기록한다.
- [ ] EndToEnd는 실제 GameStart UI 명령 → 생성 완료 → 전투 → 경험치 수집 → 선택 UI 명령 → 정상 피해 경로로 사망 → 결과 → Restart를 수행한다. 체력·결과 UI 값을 직접 맞추지 않고 동일 모델·버튼 수신 경로를 사용한다.
- [ ] EndToEnd의 경험치 단계에서 대기 → 흡수 시작 → 비행 → 접촉 지급을 각각 관측한다. PersistentWorld 시나리오는 흡수 중 청크/화면 경계 통과, RepeatedRestart는 이전 판 구슬의 늦은 표시 콜백을 포함한다. 많은 발광 구슬의 표시 비용과 흡수 중 논리 개체 수를 따로 기록한다.
- [ ] 오래된 XML·0개 테스트·Skipped·다른 실행 ID·변경된 소스 해시를 거절하는 A1 테스트를 다시 실행하고, Smoke/Stress 결과의 필수 관측 필드가 빠져도 통과하지 않게 확장한다.
- [ ] 아래 보존 검사를 기본으로 원거리 지상 이동·공중 계속 직진·경험치 왕복 확인을 구성한다. 원거리 위치도 float 변환 전에 비교한다.

```csharp
[Test]
public void LeavingTheViewDoesNotRemoveAnEnemy()
{
    using var rig = RunTestRig.Create();
    long enemy = rig.Spawn(UnitKind.Normal, new DVec2(0, 0), 1000);
    rig.PlacePlayer(new DVec2(3200, 3200));
    rig.Advance(1);
    Assert.That(rig.Unit(enemy).IsAlive, Is.True);
    Assert.That(rig.Unit(enemy).Health, Is.EqualTo(1000));
}
```

- [ ] 10분·30분 시나리오에서 논리 몬스터·경험치 수, 화면 표시 수, CPU 프레임 시간, 메모리·할당, 경로 요청·공격 예약 지연을 기록한다. 하드웨어·해상도·Editor/빌드·시드·프리셋을 함께 기록한다. 프레임률을 보고할 때 측정한 개체 수와 조건을 함께 남긴다.
- [ ] HighGrowth는 계산기용 매우 큰 BigInteger 단계와 실제 실행 가능한 단계별 복사본·반복 부하를 나눠 검사한다. 계산기만 통과했다고 실제 무한 개수 처리가 해결됐다고 하지 않는다. 수치 오류·지연을 상한이나 발동 생략으로 숨기지 않는다.
- [ ] RepeatedRestart에서 scope·구독·View·투사체·예약 이벤트 수가 새 판 기준으로 돌아오는지 비교한다. 모든 살아 있는 몬스터를 보존하는 판 내 증가와 종료한 판이 남는 누수를 구분한다.
- [ ] 관련 EditMode·PlayMode 전체를 실행하고 H01~H30을 실제 테스트/시나리오 결과에 연결한다. 실패한 항목만 관련 구현을 수정해 다시 확인한다. 성능 미달이면 공간 검색·거리장 재사용·표시 묶음을 검토하되 게임 규칙을 바꾸는 근사는 사용자와 별도로 정한다.
- [ ] Windows 개발 빌드를 `Builds/SsalMuk/`에 생성한다. BuildPlayerOptions에는 MainMenu·Battle을 명시하고 BuildReport의 성공 여부와 파일 생성을 모두 확인한다. 현재 사용자 Build Settings를 통째로 바꾸지 않는다.
- [ ] 빌드에서 EndToEnd 시나리오를 실행해 실제 플레이어 실행 파일의 결과를 확인한다. Editor 테스트 결과로 빌드 실행을 대신하지 않는다. 플레이 영상/화면 검증은 D2 결과와 연결하고 필요한 항목을 빌드에서도 확인한다.
- [ ] 완료한 작업의 체크박스와 검증 결과 경로만 계획에 기록한다. 검증 출력은 Logs 아래에 두며 요청하지 않은 log.md는 만들지 않는다. 구현·검증·자산 참조를 마지막으로 확인한 뒤 `test: verify endless survival and standalone run lifecycle`로 커밋한다.

## 단계 D와 전체 구현 완료

- [ ] H01~H30의 결과가 현재 코드·에셋에 대응한다.
- [ ] 실제 빌드에서 시작·전투·보상·사망·재시작을 확인했다.
- [ ] 무한 성장과 모든 개체 보존을 유지하며, 측정한 성능 범위와 수치 한계를 구분해 기록했다.
- [ ] 테스트나 실제 실행을 확인하지 못한 항목은 완료로 체크하지 않았다.
