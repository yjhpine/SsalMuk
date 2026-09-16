# SsalMuk C: Growth and Weapons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 경험치 수집·레벨업 선택 기회 누적·무기별 5종 강화와 4종 동시 전투를 구현한다.

**Architecture:** ProgressionService가 레벨과 선택 기회를 소유하고 RewardService가 유효한 선택만 적용한다. WeaponRuntime은 정의·강화에서 계산한 수치를 발동 시 스냅샷으로 사용한다.

경험치는 반경 진입 즉시 지급하지 않는다. ExperienceCollector가 월드의 구슬을 흡수 상태로 이동시키고 실제 몸 접촉 때만 성장 서비스에 전달한다.

**Tech Stack:** Unity `6000.4.6f1`, C#, System.Numerics.BigInteger, NUnit, MVP/uGUI.

**Spec:** [구조 설계](../specs/2026-09-16-ssalmuk-architecture-design.md) 10~11절, [전체 계획](2026-09-16-ssalmuk-implementation-plan.md), [선행 B](2026-09-16-ssalmuk-b-ai-combat.md).

## Global Constraints

- 전체 계획의 Global Constraints와 공통 타입·검증 명령을 모두 적용한다.
- `검 60도·보스 300초·선택지 3개·무기 4종·강화 5종`을 유지한다.
- 강화는 선택한 특정 무기에만 적용한다. 개수 강화는 좌우 하나씩 추가한다.
- 모든 무기를 모으면 획득 후보를 제거하고 강화만 계속 제공한다. 상한·선택 기회 유실·레벨업 일시정지는 없다.
- 표기용 큰 수 변환과 실제 판정 수치의 범위 검사를 구분한다. 기존 사용자 변경을 보존한다.

---

## Task C1: 경험치·레벨·무한 강화 데이터

**Files:** 생성 `Core/Progression/UpgradeKind.cs`, `GrowthState.cs`, `ProgressionService.cs`, `ExperienceCollector.cs`, `PickupSettings.cs`, `ExperienceTier.cs`, `GrowthService.cs`, `StatCalculator.cs`, `NumericRangeException.cs`, `NumberFormatter.cs`; 수정 `Core/Progression/WeaponState.cs`, `WeaponInventory.cs`, `Core/World/WorldStore.cs`, `ExperienceRecord.cs`, `Core/AI/CollectState.cs`, `AiContext.cs`, `Core/Units/PlayerModel.cs`, `Core/Session/RunSimulation.cs`, `Unity/Configuration/DevelopmentDefaults.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/ProgressionTests.cs`, `ExperiencePickupTests.cs`, `WeaponGrowthTests.cs`, `NumericRangeTests.cs`.

**Interfaces:** `ProgressionService.AddExperience(BigInteger)`; GrowthState의 `Level`, `ExperienceIntoLevel`, `PendingChoices`는 BigInteger. `GrowthService.Upgrade(WeaponKind,UpgradeKind)`는 소유 무기만 변경한다. `WeaponInventory.Get(WeaponKind)` → WeaponState, `WeaponState.GetLevel(UpgradeKind)` → BigInteger. `StatCalculator.Calculate(WeaponDefinition,WeaponState)` → WeaponStats. `RunTestRig.GrantExperience`, `Equip`, `Upgrade`는 실제 서비스 경로를 사용한다.

**Pickup interfaces:** `ExperienceCollector.Step(double dt)`는 RunSimulation의 생존 확인 뒤 호출한다. `PickupSettings.TestDefaults()`는 전체 계획의 임시 반경·속도·값 구분선을 반환한다. `PickupSettings.TierFor(BigInteger value)` → ExperienceTier(Green/Blue/Red). `WorldStore.TryBeginAttraction(Guid runId,long id)`, `TryCollectExperience(Guid runId,long id,out BigInteger value)` → bool은 해당 판의 유효한 상태 전환만 허용하고, `MoveExperience(Guid runId,long id,WorldPosition position)`은 흡수 레코드 위치·공간 색인을 함께 갱신한다. 이 메서드는 C1에서 작성하며 Collected 지급 후 레코드를 제거한다. AI 설정 전달에는 같은 PickupSettings를 사용한다.

- [x] 한 번의 큰 경험치 입력에서 여러 레벨과 선택 기회가 생기는 테스트, 비소유 무기 강화 거절, 아래 좌우 개수 테스트를 작성한다.

```csharp
[Test]
public void EachCopyUpgradeAddsOneWeaponOnEachSide()
{
    using var rig = RunTestRig.Create();
    rig.Upgrade(WeaponKind.Sword, UpgradeKind.Copies);
    rig.Upgrade(WeaponKind.Sword, UpgradeKind.Copies);
    var state = rig.Player.Weapons.Get(WeaponKind.Sword);
    var stats = StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword), state);
    Assert.That(stats.Copies, Is.EqualTo(new BigInteger(5)));
    Assert.That(state.GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
}
```

- [x] 근접 시작과 실제 지급을 분리하는 아래 테스트를 먼저 작성하고 실패를 확인한다. 기본 Rig의 플레이어 시작 위치는 (0,0), PickupSettings는 전체 계획의 테스트 프리셋을 사용한다.

```csharp
[Test]
public void XpIsGrantedOnceOnContactNotWhenAttractionStarts()
{
    using var rig = RunTestRig.Create();
    long id = rig.DropXp(new DVec2(1.2, 0), BigInteger.One);
    rig.Advance(0.02);
    Assert.That(rig.Run.World.TryGetExperience(id, out var orb), Is.True);
    Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
    Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.Zero));
    rig.Advance(1);
    Assert.That(rig.Run.World.TryGetExperience(id, out _), Is.False);
    Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
    rig.Advance(1);
    Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
}
```

- [x] ExperienceCollector는 Grounded 구슬이 흡수 반경에 들어오면 Attracting으로 전환하고 현재 플레이어 위치를 향해 이동시킨다. 접촉 거리(플레이어 몸 반경+구슬 반경)는 흡수 시작 거리와 분리한다. 값은 유지하고 반경 진입만으로 지급하지 않는다. 한 번 흡수를 시작하면 반경을 다시 벗어나도 따라오며 구조물·몬스터에 막히지 않는다.
- [x] 흡수 시작과 실제 접촉은 플레이어·구슬의 이동 전후 구간을 사용해 고속 교차를 검사한다. 이번 주기에 생성되거나 흡수를 시작한 구슬은 생성/시작 이후 구간만 판정한다. 접촉이 확인된 ID만 TryCollectExperience 성공 후 실제 ProgressionService에 전달한다. 이미 수집됨·잘못된 RunId·사망/종료 상태는 지급하지 않는다. 표시 애니메이션 완료는 지급 경로로 사용하지 않는다.
- [x] 이동하는 구슬의 WorldPosition과 청크/공간 색인을 함께 갱신한다. 흡수 중 표시가 사라져도 월드에서 비행을 계속한다. CollectState는 대기 구슬의 흡수 반경 안에서 도달 가능한 바닥 지점을 찾고, 목표가 Attracting이 되면 해제해 이미 날아오는 구슬을 쫓지 않는다.
- [x] 공중이 구조물 내부에서 사망한 경우 드롭 생성 시 가장 가까운 이동 가능한 바닥으로 위치를 정한 뒤 대기 위치를 보존한다. 드롭 값이나 개수를 줄이지 않는다. 색은 실제 값으로 구분하며 임시 구분선은 5·25다. 양수 값·증가하는 구분선·양수 속도·양수 구슬 반경·접촉 거리보다 큰 흡수 반경을 설정 검증에 포함한다.
- [x] 이동하는 플레이어 추적, 흡수 반경 재이탈, 구조물 가로지르기, 고속 통과, 사망과 접촉이 같은 주기인 경우, 중복 접촉, 청크 경계 통과, 이전 RunId 지급 거절을 확인한다. 값 1/5/25와 경계값 4/24의 색 등급을 검사하고 큰 값도 원본 경험치 양을 유지하는지 확인한다. EditMode의 ExperiencePickupTests·ProgressionTests를 실행해 성공을 확인한 뒤 다음 성장 단계로 진행한다.
- [x] 임시 성장 프리셋의 다음 레벨 비용은 현재 레벨 L에서 `5 + 3 * (L - 1)`로 둔다. n회 레벨업 누적 비용은 아래 식으로 계산하고 지수 탐색+이진 탐색으로 가능한 n을 찾는다. 큰 경험치 입력을 레벨 수만큼 반복하는 루프로 처리하지 않는다.

```csharp
BigInteger CostForLevels(BigInteger level, BigInteger count)
{
    return count * (5 + 3 * (level - 1)) + 3 * count * (count - 1) / 2;
}
```

- [x] 수집 경험치에서 정확한 비용을 차감하고 Level과 PendingChoices에 n을 더한다. PlayerModel.Level은 Growth.Level을 읽도록 연결해 HUD·사망 결과가 별도 레벨 값을 갖지 않게 한다. 임시 계수는 DevelopmentDefaults에서 편집할 수 있게 구성하고 함수는 전달된 계수를 사용한다. 위 코드는 테스트 프리셋의 계산 예다.
- [x] 강화 단계를 BigInteger로 보관하고 기본 정의는 변경하지 않는다. 임시 성장식은 Damage=`base*(1+0.10*level)`, Copies=`1+2*level`, Repeats=`1+level`, Period=`base/(1+0.10*level)`, Range=`base*(1+0.08*level)`로 지정한다. 이 계수는 최종 밸런스가 아니며 정의 에셋에서 조정한다.
- [x] 정수 단계→double 계산을 한 곳으로 모으고 NaN·Infinity·0 이하 주기·강화 후 증가 소실을 NumericRangeException으로 검출한다. 예외를 게임 속 최대 레벨로 바꾸지 않는다. 큰 단계에서 계산기와 실제 스케줄러의 처리량은 각각 D3에서 확인한다.
- [x] 표시 문자열은 BigInteger 원본으로 생성한다. 짧은 값은 정수, 긴 값은 앞자리와 자릿수로 표현하되 실제 저장값은 줄이지 않는다. 사망과 새 판에서 GrowthState·WeaponState를 새로 만드는 테스트를 추가한다.
- [x] 5종 강화 각각의 독립성, 모든 무기별 범위 해석, 큰 경험치 입력, 큰 단계의 범위 오류 검출을 확인한다. `feat: add experience progression and uncapped weapon growth`로 커밋한다.

## Task C2: 가중 선택지·누적 기회·전투 중 UI

**Files:** 생성 `Core/Progression/RewardId.cs`, `RewardWeights.cs`, `OfferSnapshot.cs`, `OfferGenerator.cs`, `RewardCommand.cs`, `RewardService.cs`, `Core/Session/IRunCommands.cs`; 생성 `Presentation/LevelUpPresenter.cs`, `ViewContracts/ILevelUpView.cs`, `Unity/Views/LevelUpView.cs`; 수정 `Core/Session/RunSimulation.cs`, `RunModel.cs`, `Unity/Bootstrap/RunScope.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/RewardTests.cs`, `Tests/PlayMode/LevelUpLiveCombatTests.cs`.

**Interfaces:** `RewardId.Acquire(WeaponKind)`, `RewardId.Upgrade(WeaponKind,UpgradeKind)`는 값 동등성이 있는 ID이며 `IsUpgrade` 읽기 속성을 제공한다. `OfferSnapshot.Id`, `Choices`, `OwnershipVersion`은 읽기 전용. `OfferGenerator.Generate(IReadOnlyCollection<WeaponKind>,IRandomSource,RewardWeights)` → `IReadOnlyList<RewardId>` 3개. `RewardWeights.TestDefaults()`는 범주 1:3 설정을 반환한다. `IRunCommands.TryQueueChoice(Guid runId,long offerId,int slot)` → bool. RunTestRig의 CurrentOffer·Choose는 이 읽기/명령 경로를 사용한다.

- [x] 검만 보유·두 무기·네 무기 상태의 후보 구성과 3개 중복 방지를 고정 시드로 검증한다.

```csharp
[Test]
public void OwnedWeaponsNeverAppearAsAcquisitionRewards()
{
    var owned = new[] { WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Axe, WeaponKind.Fireball };
    var offer = new OfferGenerator().Generate(owned, new SeededRandom(7), RewardWeights.TestDefaults());
    Assert.That(offer.Count, Is.EqualTo(3));
    Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
    Assert.That(offer.All(x => x.IsUpgrade), Is.True);
}
```

- [x] 후보 집합은 미보유 획득+보유 무기별 5강화다. 범주를 가중 선택한 뒤 해당 범주의 항목을 선택하고 임시 후보에서 그 항목을 제거한다. 빈 범주를 제외해 매 추출마다 정규화한다. 임시 프리셋은 획득/강화 범주 가중치 1:3, 범주 안 항목은 동일 가중치다. 서로 다른 유효 후보 3개를 만들 수 없는 설정은 전투 전 오류로 검출한다.
- [x] 현재 선택지는 추가 레벨업에도 유지하고 아직 표시하지 않을 선택 기회는 개수로 보관한다. PendingChoices가 양수이고 CurrentOffer가 없을 때만 새 OfferId로 생성한다.
- [x] 선택 명령은 주기 시작에 RunId·OfferId·소유 버전·slot을 검사해 한 번만 적용한다. 소유 상태 갱신 → 기회 1회 소비 → 다음 선택지 생성 순서로 처리한다. 이중 클릭의 나중 명령, 종료한 판, 범위를 벗어난 slot은 상태를 바꾸지 않는다.
- [x] LevelUpPresenter에 세 버튼과 남은 기회 수를 연결한다. 화면을 여닫아도 timeScale·RunClock·BattleRunner를 변경하지 않는다. 선택 화면이 떠 있는 동안 적의 이동·피격·경험치 수집을 관측하는 PlayMode 테스트를 작성한다.

```csharp
[Test]
public void ExtraLevelsPreserveTheCurrentOffer()
{
    using var rig = RunTestRig.Create();
    rig.GrantExperience(5);
    rig.Advance(0.02);
    long first = rig.CurrentOffer.Id;
    rig.GrantExperience(100);
    rig.Advance(0.02);
    Assert.That(rig.CurrentOffer.Id, Is.EqualTo(first));
    Assert.That(rig.Player.Growth.PendingChoices, Is.GreaterThan(BigInteger.One));
}
```

- [x] 신규 무기를 선택한 뒤 다음 후보에 해당 무기 강화가 추가되고 획득 후보가 제거되는지 확인한다. 선택 직전 사망·동일 프레임 다중 클릭·대량 레벨업·모든 무기 보유 상태를 검증한다. 실제 전투 중 UI까지 확인한 뒤 `feat: add live level-up choices and queued rewards`로 커밋한다.

## Task C3: 창·도끼·파이어볼과 개수·연속·속도 반영

**Files:** 생성 `Core/Combat/SpearAttack.cs`, `AxeAttack.cs`, `FireballAttack.cs`, `ProjectileModel.cs`, `ProjectileSystem.cs`, `CopyLayout.cs`, `TargetCircle.cs`; 수정 `Core/Combat/AttackGeometry.cs`, `AttackScheduler.cs`, `WeaponRuntime.cs`, `Core/Session/RunSimulation.cs`, `Unity/Configuration/GameCatalog.cs`, `Tests/Shared/RunTestRig.cs`; 생성 `Tests/EditMode/MeleeGeometryTests.cs`, `FireballTests.cs`, `WeaponSchedulingTests.cs`, `Tests/PlayMode/MultiWeaponCombatTests.cs`.

**Interfaces:** `CopyLayout.Offset(WeaponKind,BigInteger copyIndex,double spacing)` → DVec2 또는 도끼 위상. `TargetCircle(long id,DVec2 center,double radius)`; `AttackGeometry.FirstCircleHit(DVec2 start,DVec2 end,IReadOnlyList<TargetCircle>)` → `long?`. `ProjectileSystem.Step(double from,double to)`는 실제 WorldQuery·DamageService를 사용한다. 무기 획득은 기존 WeaponInventory, 강화는 GrowthService를 사용한다.

- [x] 전방 창의 길이·폭, 도끼 한 바퀴의 전후좌우 타격과 1회 제한, 파이어볼 최초 접촉 순서 테스트를 작성한다.

```csharp
[Test]
public void ProjectileChoosesTheFirstHitAlongItsPath()
{
    var targets = new[] {
        new TargetCircle(20, new DVec2(4, 0), 0.3),
        new TargetCircle(10, new DVec2(1, 0), 0.3)
    };
    Assert.That(AttackGeometry.FirstCircleHit(new DVec2(0, 0), new DVec2(6, 0), targets),
        Is.EqualTo(10));
}
```

- [x] 창은 전진하는 선분에 폭을 더한 형상, 도끼는 이전·현재 위상 사이의 원형 궤적으로 구현한다. 같은 복사본·반복에서 대상별 한 번만 피해를 준다. 여러 무기의 중복은 서로 다른 HitKey이므로 정상 피해로 인정한다.
- [x] 복사본 인덱스 0은 중앙, 이후는 왼쪽1·오른쪽1·왼쪽2·오른쪽2 순서로 정의한다. 검·창·파이어볼은 목표 방향의 수직 오프셋, 도끼는 같은 반경의 대칭 위상으로 배치한다. Copies를 int로 잘라 배열을 만드는 대신 인덱스를 순회 가능한 표현으로 유지한다.
- [x] 반복 횟수 R의 발동은 묶음 주기 P의 앞 60%에 균등 배치한다. R=1이면 시작 시 1회다. 각 반복 때 목표를 재평가하고 복사본은 그 목표 방향을 공유한다. 예약된 묶음의 횟수·시각은 유지하고 속도·반복 강화는 다음 묶음, 피해·범위·개수 강화는 다음 실제 발동에서 반영한다.
- [x] 파이어볼은 무비용으로 발사하고 직선 이동의 최초 원형 접촉 위치에서 소멸·폭발한다. 목표의 이동도 상대 구간으로 고려한다. 폭발은 범위 쿼리로 DamageService에 대상별 1회만 전달한다. 직격 피해를 따로 더하지 않는다. 구조물은 검사 대상에서 제외한다.
- [x] 명중하지 않은 투사체는 WeaponDefinition의 비행 수명 종료 시 피해 없이 정리한다. 화면 이탈만으로 제거하지 않는다. 발동 이후 수치·방향은 ProjectileModel에 저장해 강화나 목표 사망으로 바뀌지 않게 한다.
- [x] 테스트 프리셋에서 체력 20인 두 적을 폭발 안에 놓고 파이어볼 피해 6이 각각 한 번만 들어가는지 검사한다. 범위 밖 대상, 고속 투사체, 공중 대상, 같은 위치 다중 대상, 발사 직후 강화도 포함한다.
- [x] 네 무기를 실제로 동시에 장착하고 개수·반복·속도를 각각 올려 다른 효과가 발생하는지 확인한다. 효과를 간략 표시하더라도 논리 타격을 생략하지 않는다. 통과 후 `feat: add spear axe fireball and scalable attack scheduling`으로 커밋한다.

## 단계 C 완료

- [x] 실제 전투가 계속되는 상태에서 3개 선택·기회 누적·획득 후보 갱신을 확인했다.
- [x] 무기별 5종 강화가 모두 해당 무기의 판정·스케줄에 반영된다.
- [x] 무기 4종 동시 전투와 사망 후 초기화가 통과하며 D1로 진행한다.
