# SsalMuk B: AI and Combat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** low AI의 수집·회피·돌파 판단과 검 자동 전투, 피격·사망·재시작을 연결한다.

**Architecture:** 운영 FSM은 이동·목표 입력만 생성한다. RunSimulation이 이동·공격·피해·종료를 정해진 순서로 처리하고 Presenter가 표시한다.

**Tech Stack:** Unity `6000.4.6f1`, C# Core, NUnit, Unity Test Framework `1.6.0`, MVP.

**Spec:** [구조 설계](../specs/2026-09-16-ssalmuk-architecture-design.md) 8~10·13절, [전체 계획](2026-09-16-ssalmuk-implementation-plan.md), [선행 A](2026-09-16-ssalmuk-a-foundation-world.md).

## Global Constraints

- 전체 계획의 Global Constraints와 공통 타입·검증 명령을 모두 적용한다.
- `C:/Users/ace21/Desktop/SsalMuk`, Unity `6000.4.6f1`을 유지한다.
- 시작 무기는 검이며 전방 `60도`를 유지한다. 공격 때문에 이동·보행을 시작하거나 정지시키지 않는다.
- 보상 UI가 추가되어도 전투를 멈추지 않을 구조로 만든다. 넉백·피격 연출·무적은 서로 다른 상태다.
- 원거리 몬스터·경험치 보존, 사용자 변경 보존, 로컬 커밋만 수행하는 규칙을 지킨다.

---

## Task B1: low AI와 공통 공격 목표

**Files:** 생성 `Core/AI/BrainState.cs`, `IBehaviorState.cs`, `AiContext.cs`, `AiSettings.cs`, `LowAiController.cs`, `CollectState.cs`, `EvadeState.cs`, `BreakoutState.cs`, `TargetResolver.cs`; 수정 `Core/Units/PlayerModel.cs`, `Core/Navigation/NavigationService.cs`, `Unity/Bootstrap/BattleRunner.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/LowAiTests.cs`, `TargetResolverTests.cs`.

**Interfaces:** `BrainState.Collect/Evade/Breakout`; `IBehaviorState.Enter(AiContext)`, `Tick(AiContext,double)`, `Exit(AiContext)`; AiContext는 Actor(UnitModel)·월드 조회·길찾기·설정을 제공하고 PlayerModel로 고정하지 않는다. `LowAiController.Tick(double dt)`; `PlayerModel.MoveIntent`, `BrainState`, `TargetId`, `BreakoutDirection`은 읽기 속성. `TargetResolver.Resolve(PlayerModel,IWorldQuery)` → `long?`. `RunTestRig.DropXp(DVec2,BigInteger)`는 A3 WorldStore에 실제 레코드를 추가한다.

- [ ] 가까운 적이 화면 밖/다른 청크에 있는 경우, 거리 동률, 목표 사망 후 교체 테스트를 작성한다. 거리와 ID 순서가 같은 입력에서 같은 목표를 만드는지 확인한다.
- [ ] 다음 포위 테스트와 위험한 경험치·긴급 충돌을 구분하는 테스트를 작성하고 실패를 확인한다.

```csharp
[Test]
public void EncirclementCommitsToAnEscapeDirection()
{
    using var rig = RunTestRig.Create();
    rig.PlacePlayer(new DVec2(0, 0));
    for (int i = 0; i < 16; i++) {
        double a = i * Math.PI / 8;
        rig.Spawn(UnitKind.Normal, new DVec2(Math.Cos(a), Math.Sin(a)));
    }
    rig.Advance(0.02);
    Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
    Assert.That(rig.Player.BreakoutDirection.Length, Is.EqualTo(1).Within(1e-6));
}
```

- [ ] AiSettings에 탐색 거리·긴급 접촉 시간·포위 판단 방향 수·차단 비율·목표 유지 시간·해제 안정 시간·진행 없음 시간을 둔다. 테스트 프리셋은 위 반경 1의 16방향 배치를 포위로 분류하게 명시한다. 실제 밸런스 에셋과 테스트용 수치를 혼동하지 않는다.
- [ ] Collect는 경험치 가치에서 이동 거리와 위험 비용을 차감해 목표를 고른다. 공격적 운영의 위험 가중치는 양수로 두되 위험 지역을 모두 금지하지 않는다. 가장 높은 점수 목표가 이동 불가이면 다음 유효 후보를 선택한다. 경험치가 없으면 가까운 위협과 간격을 유지하거나 정지한다.
- [ ] Evade는 이웃 적의 상대 위치·접근 속도로 예상 접촉을 평가하고, 이동 가능한 방향 후보 중 위험을 낮추는 방향을 고른다. 앞에 구조물이 있으면 sweep 검사 결과로 후보를 배제한다.
- [ ] Breakout은 방향별 장애물·적 밀도·앞을 막는 체력·통과 거리를 평가하고 방향을 보존한다. 진행 없음이나 구조물 차단만 재선정 조건으로 삼는다. 일반 상태로 돌아가기 위한 낮은 차단 임계값과 안정 시간을 분리한다.
- [ ] 목표 resolver는 Breakout 통로를 막는 살아 있는 적을 우선하고 없으면 가장 가까운 적을 반환한다. 상태 진입·종료와 전환 이유를 기록한다. 공격 실행기나 Unity Transform을 FSM에서 호출하지 않는다.
- [ ] RunTestRig와 BattleRunner에서 AI → MovementSystem 순서로 연결한다. 포위 해제 뒤 기본 목표 복귀·긴급 회피·위험 경험치 접근·방향 진동 없는 시나리오를 확인한 뒤 `feat: add aggressive collection and breakout AI`로 커밋한다.

## Task B2: 피해·접촉·넉백·경험치 드롭

**Files:** 생성 `Core/Combat/HitKey.cs`, `DamageRequest.cs`, `DamageService.cs`, `ContactDamageSystem.cs`, `KnockbackState.cs`, `CombatEvent.cs`, `DeathService.cs`; 수정 `Core/Units/UnitModel.cs`, `PlayerModel.cs`, `Core/Movement/MovementSystem.cs`, `Core/World/WorldStore.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/DamageTests.cs`, `ContactDamageTests.cs`, `DeathDropTests.cs`.

**Interfaces:** `HitKey(Guid runId,long attackId,BigInteger copy,BigInteger repeat)`; `DamageRequest`는 HitKey·SourceId·TargetId·Amount·Direction·넉백 세기·시간을 담는다. `DamageService.TryApply(DamageRequest,double now)` → bool. `ContactDamageSystem.Step(double from,double to)`; `UnitModel.InvulnerableUntil`, `Knockback`는 읽기만 공개. `RunTestRig.Hit(long targetId,double amount)`는 새 HitKey의 실제 DamageService 요청을 전달한다.

- [ ] 같은 타격 키의 중복 요청, 여러 접촉과 짧은 무적, 적 사망 중복 요청 테스트를 작성한다. 같은 정의를 공유하는 두 적 중 하나에게만 피해를 줘 다른 개체와 정의의 체력이 유지되는지도 확인한다.

```csharp
[Test]
public void InvulnerabilityRejectsTheNextHit()
{
    using var rig = RunTestRig.Create();
    double before = rig.Player.Health;
    Assert.That(rig.Hit(rig.Player.Id, 3), Is.True);
    Assert.That(rig.Hit(rig.Player.Id, 3), Is.False);
    Assert.That(rig.Player.Health, Is.EqualTo(before - 3));
}
```

- [ ] DamageService에 생존·판 유효성·양수 피해·HitKey 중복·플레이어 무적 검사 순서를 구현한다. 수락한 피해만 체력을 바꾸고 단일 CombatEvent를 발생시킨다. 플레이어에게 수락된 피해는 즉시 무적 종료 시각을 설정한다.
- [ ] 접촉 후보는 이동 전후 몸체와 이동 구간으로 수집한다. 접촉 시점→적 ID 순서로 처리한다. 실제 접촉이 계속되면 무적 종료 후 다시 피해를 받을 수 있게 하며, 동일 주기에 사망한 적은 접촉 후보에서 제외한다.
- [ ] 넉백을 이동 주기에 합성하고 구조물 sweep와 군집 보정을 통과시킨다. 반복 피격은 최신 넉백 방향·세기·종료 시각으로 갱신한다. 공중의 원래 비행 방향은 보존하고 넉백으로 덮어쓰지 않는다. 실제 공중 스폰·비행 연결은 D1이다.
- [ ] DeathService는 살아 있음→사망 전환을 한 번만 수락하고 처치 수 증가·경험치 드롭·논리 적 제거를 한 번 수행한다. WorldStore의 경험치 저장을 재사용한다. 피해 처리 도중 컬렉션을 순회하며 즉시 제거하지 말고 해당 단계의 완료 큐로 반영한다.
- [ ] 무적 종료 직전·직후, 두 공격이 동시에 치명 피해를 주는 상황, 구조물 옆 넉백, 화면 밖 사망 드롭을 검증한다. 테스트 체력은 팩토리 생성 입력으로 설정한다. 통과 후 `feat: implement damage knockback and single death drops`로 커밋한다.

## Task B3: 검 자동 전투와 고정 주기 처리

**Files:** 생성 `Core/Combat/WeaponDefinition.cs`, `WeaponStats.cs`, `WeaponRuntime.cs`, `AttackInstance.cs`, `AttackGeometry.cs`, `SwordAttack.cs`, `AttackScheduler.cs`, `Core/Session/RunSimulation.cs`; 수정 `Core/Units/DefinitionCatalog.cs`, `Unity/Configuration/GameCatalog.cs`, `Unity/Bootstrap/RunScope.cs`, `BattleRunner.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/SwordAttackTests.cs`, `AttackSchedulerTests.cs`, `SimulationOrderTests.cs`.

**Interfaces:** `RunSimulation.Step(double dt)`; `WeaponRuntime.Tick(double from,double to)`; `AttackGeometry.SwordContains(DVec2 relative,double targetRadius,double reach)`; WeaponStats에는 Damage·Range·PeriodSeconds·BigInteger Copies/Repeats·BurstFraction을 둔다. AttackInstance는 HitKey·발동 시각·방향·스탯 스냅샷·형상 진행률을 보관한다. `DefinitionCatalog.GetWeapon(WeaponKind)`를 추가한다.

- [ ] 검의 60도 경계와 근접 원형 대상, 같은 휘두르기 중복 피해 테스트를 작성한다.

```csharp
[Test]
public void SwordCoversForwardSixtyDegrees()
{
    Assert.That(AttackGeometry.SwordContains(new DVec2(1, 0), 0, 2), Is.True);
    Assert.That(AttackGeometry.SwordContains(new DVec2(1, 1), 0, 2), Is.False);
    Assert.That(AttackGeometry.SwordContains(new DVec2(-1, 0), 0, 2), Is.False);
}
```

- [ ] WeaponDefinition에 기본 피해·범위·주기·휘두르기 시간과 표시 키를 정의하고 검을 초기 소유 무기에 연결한다. 목표가 없을 때 빈 공격을 적립하지 않고 준비 상태를 유지한다.
- [ ] AttackScheduler는 다음 발동 시각을 보존한다. 한 주기에 여러 발동이 도래하면 발동별 HitKey를 만들고 모두 처리한다. 공격 주기보다 프레임이 길어도 1회로 잘라 버리지 않는다. 강화 계산기는 C1에서 연결하므로 현재는 정의에서 만든 WeaponStats를 사용한다.
- [ ] 검은 이전·현재 진행 각도 사이의 쓸기 구간으로 적 후보를 판정한다. 원형 대상과 부채꼴의 접촉은 중심 포함뿐 아니라 양쪽 변·외곽 원호까지 검사한다. 한 휘두르기에서 이미 맞은 대상 ID를 저장하고 종료 뒤 정리한다.
- [ ] RunSimulation의 최종 순서는 아래와 같이 정한다. 현재 존재하는 이동·공격·피해·사망 단계부터 호출하고, C1에서 수집 단계, C2에서 맨 앞의 명령 단계를 실제 서비스로 추가한다. 아직 구현하지 않은 기능을 성공 이벤트로 위장하지 않는다.

```text
유효한 명령 → 시계 → 스폰 예약 → AI와 이동 → 무기와 적 피해
→ 적 사망과 경험치 드롭 → 생존 적 접촉 피해 → 플레이어 사망 판정
→ 살아 있을 때 경험치 수집 → 표시 이벤트
```

- [ ] BattleRunner의 FixedUpdate 하나만 RunSimulation을 호출하게 교체한다. 개별 유닛의 Update/FixedUpdate로 같은 모델을 다시 움직이지 않는다. 정지 중 공격·이동 중 공격·살아 있는 피격 중 공격을 모두 확인한다.
- [ ] 검으로 같은 주기에 죽인 적의 접촉 피해 제외, 목표 없는 시간 뒤 공격 폭주 없음, 큰 dt에서 발동 누락 없음, 발동 후 수치·방향 스냅샷 유지 테스트를 통과시킨다. `feat: add sword attacks and ordered battle simulation`으로 커밋한다.

## Task B4: 체력 HUD·사망 결과·완전한 새 판

**Files:** 생성 `Core/Session/RunResult.cs`, `Presentation/HudPresenter.cs`, `ResultsPresenter.cs`, `ViewContracts/IHudView.cs`, `IResultsView.cs`, `Unity/Views/HudView.cs`, `ResultsView.cs`; 수정 `Core/Session/RunCoordinator.cs`, `RunModel.cs`, `RunSimulation.cs`, `ISceneLoader.cs`, `Unity/Bootstrap/RunScope.cs`, `AppRoot.cs`, `UnitySceneLoader.cs`, `Tests/Shared/RunTestRig.cs`, `Tests/PlayMode/SessionLifecycleTests.cs`; 생성 `Tests/EditMode/RunResultTests.cs`, `Tests/PlayMode/DeathRestartTests.cs`.

**Interfaces:** `RunResult`는 immutable RunId·SurvivalSeconds·KillCount·BigInteger FinalLevel. `RunCoordinator.RestartAsync()` → Task, `ReturnToMenuAsync()` → Task. `ISceneLoader.LoadMainMenuAsync()`를 추가하고 UnitySceneLoader와 테스트 대역에 함께 구현한다. `RunTestRig.Result`는 종료 스냅샷이며 `RestartAsync()`는 실제 coordinator에 테스트용 scene loader를 주입해 호출한다. HudPresenter.Refresh는 읽기 모델만 받는다.

- [ ] 사망 뒤 결과 스냅샷이 성장 초기화와 독립적인 테스트와 아래 새 판 테스트를 작성한다.

```csharp
[Test]
public async System.Threading.Tasks.Task RestartCreatesANewRun()
{
    using var rig = RunTestRig.Create();
    Guid previous = rig.Run.Id;
    rig.Hit(rig.Player.Id, rig.Player.Health + 1);
    rig.Advance(0.02);
    Assert.That(rig.Result.RunId, Is.EqualTo(previous));
    await rig.RestartAsync();
    Assert.That(rig.Run.Id, Is.Not.EqualTo(previous));
    Assert.That(rig.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
}
```

- [ ] 치명 피해 뒤 RunSimulation이 추가 접촉·수집·공격을 중지하도록 한다. RunCoordinator는 결과를 복사하고 살아 있는 판의 명령·예약·구독·View를 종료한다. 화면이 읽을 Result는 scope와 함께 지우지 않는다.
- [ ] MVP HUD에서 실제 체력·경험치 자리·레벨·시간·처치 수를 표시한다. 레벨은 이 단계에서 1이며 성장 연결은 C1이다. 적 체력 표시도 모델을 읽어 두 종류 이상의 유닛이 실제 피해를 받는지 확인한다.
- [ ] ResultsView에 생존 시간·처치 수·최종 레벨, Restart·MainMenu 버튼을 연결한다. 버튼 중복을 잠그고 다시 시작은 새로운 scope·시드·ID·검 상태로 A5 생성 순서를 다시 수행한다.
- [ ] 늦게 완료된 이전 씬 요청·피격 이벤트가 새 RunId에 적용되지 않는 테스트를 추가한다. 로드 실패 시 기존 결과를 보존하고 메뉴에서 다시 시작할 수 있게 한다.
- [ ] Editor에서 검 전투 → 사망 결과 → 다시 시작 → 메인 메뉴 → GameStart를 실제 반복한다. Domain Reload 설정 변화와 구독·scope 수를 확인한다. 통과 후 `feat: add battle HUD results and clean restart`로 커밋한다.

## 단계 B 완료

- [ ] AI 판단·검 피해·짧은 무적·넉백·사망과 재시작을 실제 연결했다.
- [ ] 수집 판단용 경험치가 존재하고, 성장 선택은 C 단계의 남은 작업으로 명시한다.
- [ ] 관련 테스트와 실제 검 전투 확인 후 C1로 진행한다.
