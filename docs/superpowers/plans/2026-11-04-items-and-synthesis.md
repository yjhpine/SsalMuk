# Items and synthesis implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 승인된 24종 패시브 아이템, 상자 선택, 18개 레시피 합성과 인벤토리 UI를 실제 전투에 연결한다.
**Architecture:** 순수 Core가 정의·보유·원자적 명령·효과를 소유하고 기존 런타임 uGUI가 표시와 입력을 담당한다. 레벨업과 상자 선택은 한 번에 하나만 표시하며 기존 바닥 버프는 유지한다.
**Tech Stack:** Unity 6000.4.6f1, C#, uGUI, NUnit, 프로젝트 검증 하네스.
**Spec:** `docs/superpowers/specs/2026-11-04-items-and-synthesis-design.md` (2026-11-04 `진행`으로 승인)

## Global Constraints

- 사용자 지정 `C:/Users/ace21/Desktop/SsalMuk`의 기존 `codex/unit-foundation` 브랜치에서 진행한다.
- 회색 커먼 → 초록 언커먼 → 파랑 레어 → 노랑 레전더리 → 빨강 얼티밋.
- 같은 등급의 서로 다른 소유 인스턴스 3개 → 다음 등급 1개. 동일 정의 중복 허용.
- 전투 계속, ESC 정지, 상자 수동, 레벨업 기존 5초 AUTO 유지.
- 24개 정의·18개 레시피·계수는 Spec 3–4절 그대로 적용한다.
- 일반·보스·경험치 보존, 바닥 버프, 상자 위치·AI, 기존 강화 계약을 유지한다.
- 별도 생존 테스트·전체 회귀·장시간 검사·빌드·푸시·외부 에셋 도입은 이번 범위에서 제외한다.
- 검증된 작업만 로컬 커밋하며 기존 Recovery/ProfilerCaptures 파일을 보존한다.

## Review Focus

- 합성 실행 직전에 보상이 지급돼 버전이 바뀐 요청은 재료를 잃지 않는다 (Task 2).
- 여러 상자와 레벨이 한 주기에 쌓여도 현재 선택이 덮어써지지 않는다 (Task 2).
- 보호막 재료를 합성해 다른 보호막을 얻어도 즉시 완충되지 않는다 (Task 3).
- 긴 인벤토리를 스크롤한 뒤 재료를 소모해도 다른 인스턴스를 잘못 선택하지 않는다 (Task 4).
- 파생 피해로 처치해도 보상은 한 번이며 추가 아이템 효과는 연쇄 발동하지 않는다 (Task 3).

## Task 1: Definitions, inventory and atomic fusion

**Files:** create `Core/Items/{ItemDefinition,ItemCatalog,ItemDefaults,ItemInventory,FusionService}.cs`; modify `Core/Session/RunModel.cs`, `Core/Units/PlayerModel.cs`; test `Tests/EditMode/ItemInventoryTests.cs`.
**Interfaces:** `ItemCatalog.Default()`; `ItemInventory.Items`, `.Version`, `.Bonuses`, `.TryAdd(string)`; `FusionService.Preview(IReadOnlyList<long>)`, `.TryQueue(FusionCommand)`, `.Step()`; command carries run ID, inventory version, three IDs, mode, expected recipe.

- [ ] Write failing behavior tests: three identical materials allowed for random; recipe count/order independent; mixed/duplicate-instance/ultimate/stale requests preserve contents and random stream; successful fusion removes bonuses and appends result exactly once.
- [ ] Run `validate.ps1 -Mode EditMode -Filter SsalMuk.Tests.ItemInventoryTests`; confirm expected failures before implementation.
- [ ] Add validated catalog, immutable definitions, cached bonus totals, chronological IDs, preflight atomic replacement and seeded fusion stream. Validate every possible random outcome before consuming randomness.
- [ ] Run the same filter; require all tests passed and current manifest/receipt/XML.
- [ ] Commit this independently testable domain.

## Task 2: Chest reward tickets and stat integration

**Files:** create `Core/Items/ItemRewardService.cs`, `Unity/Configuration/ItemCatalogSettings.cs`; modify `RunModel`, `RunSimulation`, `RewardService`, `ChestSystem`, `ExperienceCollector`, `PowerupSystem`, AI pickup-radius consumers, `GameCatalog`, `RunScope`; tests `ItemRewardTests`, `ItemStatTests`, existing `ChestExplorationTests`.
**Interfaces:** `ItemRewardService.CurrentOffer`, `.PendingCount`, `.AddChest(long, ItemRarity)`, `.TryQueueChoice(Guid,long,int)`, `.Step()`; `RunModel.HasItemChoice`; `ChestRecord.Rarity`; `PlayerModel.Inventory` provides cached bonuses.

- [ ] Write failing tests: deterministic chest grade and frozen distinct candidates; current level remains; next chest precedes next level; no extra level granted; paused/stale/duplicate choice denied; award invalidates queued fusion. XP 1 with +5% and +10% gives 115 hundredths; item damage/speed/pickup stack with existing upgrades.
- [ ] Run exact new test filters and observe missing behavior.
- [ ] Add independent grade/offer/fusion randomness. Remove old temporary chest-upgrade route. Process level/chest commands then fusion each step, with current-offer arbitration.
- [ ] Connect tunable catalog data through DevelopmentDefaults/GameCatalog and initialize each run. Apply item multipliers to actual weapon, movement, healing, pickup and exact XP consumers.
- [ ] Run new filters plus ChestExploration and relevant shared-growth/level-up tests; confirm preserved placement and level rules.
- [ ] Commit reward/stat integration.

## Task 3: Combat effects

**Files:** create `Core/Items/ItemEffectSystem.cs`; modify `DamageRequest`, `CombatEvent`, `DamageService`, `RunSimulation`, combat presentation snapshots; test `ItemEffectTests`.
**Interfaces:** `DamageOrigin` direct/item; `CombatEvent.ActualHealthDamage`; `ItemEffectSystem.Tick()`, `.ProcessHits()`; reusable read-only visual snapshots; shield state on player inventory effect runtime.

- [ ] Write failing tests for actual-damage lifesteal/caps/.5s cooldown; nearest deterministic chain with exclusions; direct kill explosion; derived kills reward without recursion; shield first-fill, absorption/invulnerability/knockback, recharge, atomic fusion and pause/reset.
- [ ] Run ItemEffectTests and observe expected failures.
- [ ] Queue accepted direct events before death removal, process one emitter per family with shared timestamps, use DamageService attack lifecycle, apply shield after invulnerability validation.
- [ ] Use instantaneous proc damage and short pooled visual snapshots; no extra moving projectile or collider.
- [ ] Run ItemEffectTests plus related damage/death/weapon regression filters; validate evidence.
- [ ] Commit combat integration.

## Task 4: Item HUD, choice and fusion UI

**Files:** create `Unity/Views/{ItemUiStyle,ItemTooltip,InventoryView,FusionView,ItemRewardView,UpgradeSummaryView}.cs`; modify `AppRoot`, `LevelUpPresenter`, world chest/effect presentation; tests `Tests/PlayMode/ItemUiTests.cs` and affected presenter tests.
**Interfaces:** runtime views bind current run, callbacks enqueue Core commands only; visible inventory slots map current instance IDs; one shared tooltip; root orchestrates mutually exclusive choice views.

- [ ] Add tests for level AUTO suspension/reset around chest, fusion ownership/selection after scroll/consume, duplicate clicks, tooltip rarity names and UI ordering. Use real view events and run commands.
- [ ] Run targeted presenter/UI tests and observe missing behavior.
- [ ] Build left upgrade summary and right four-column virtualized inventory; chronological individual slots, reusable visible rows, colored names/borders, hover tooltip clamped to canvas.
- [ ] Build chest three-choice cards and bottom fusion panel; fixed recipe preview/random pool, reasons for disabled actions, selected IDs retained across reward overlays and revalidated. Keep any-click AUTO cancellation.
- [ ] Color world chests by rarity; pool instantaneous lightning/explosion/shield feedback without per-effect Update.
- [ ] Run targeted UI/presenter tests and Static. Inspect actual running UI and advancing clock via official Unity connection, restore stopped clean Editor.
- [ ] Commit UI and update approved spec, planning status and handoff with exact evidence limits.

## Completion

- [ ] Review changed branch with a fresh reviewer using requesting-code-review; correct important findings and rerun only relevant checks.
- [ ] Reconcile current source hashes with final Static/EditMode/PlayMode receipts and test counts, inspect git diff and commit task files only.
- [ ] Report using the user's concise completion convention.
