# Body and hit radius separation implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this approved continuation in the existing Unity checkout. Checkpoints are the failing regression, focused passing tests, and measured population comparisons.

**Goal:** Separate 80% movement bodies from 110% enemy hit areas, replace forced crowd displacement with weak steering, and measure 300/500/1000 enemies in Unity.

**Architecture:** Core keeps independent BodyRadius, HurtRadius and VisualFootOffset values. Combat broad and narrow phases use HurtRadius; contact, terrain and navigation use BodyRadius. The movement pass steers only voluntary ground-enemy motion and never iteratively relocates overlapping bodies.

**Tech Stack:** Unity 6000.4.6f1, existing C# Core/Presentation/Unity assemblies, official CLI/Pipeline, existing validation harness.

**Spec:** User-approved proposal and clarification in `../../../log.md`, section about body 80% and hit area 110%; follow-up explicitly requests implementation and 1000-enemy testing.

## Global constraints

- Work in `C:/Users/ace21/Desktop/SsalMuk`, preserve existing user settings, one Editor, no push or new build.
- Keep normal gameplay cap 500. Override population only in explicit diagnostics.
- Preserve attack shapes, damage, attack knockback, terrain blocking, air rules and logical entity persistence.
- Circular footprint uses the shorter dimension of the rendered sprite as its reference diameter; diameter ratios are 0.8 and 1.1. Keep the existing foot anchor independent of the new body radius.
- A single bounded population sample is not proof of unlimited accumulation or upgraded late-game combat performance.

## Task 1: Separate collision and hit contracts

Files: `Core/Units/UnitDefinition.cs`, `UnitModel.cs`, `Core/Combat/MeleeAttack.cs`, `ProjectileSystem.cs`, `Core/Movement/MovementSystem.cs`, `Core/Spawning/DifficultyCurve.cs`, `Unity/Configuration/DevelopmentDefaults.cs`, `Unity/Views/UnitView.cs`, diagnostic definition copies, and `Tests/EditMode/BodyHitSeparationTests.cs`.

- [x] Add validated optional `double? hurtRadius = null, double? visualFootOffset = null` constructor arguments, defaulting to the existing body radius for old isolated test fixtures.
- [x] Test actual sword/spear/axe and fireball edge hits using larger HurtRadius, a negative edge control, unchanged contact damage and difficulty-copy preservation.
- [x] Track `LargestHurtRadius` when units register; use it in attack candidate searches and use each target's HurtRadius in exact hit tests.
- [x] Add serialized `hurtRadius` and `visualFootOffset` to UnitEntry. Use visual foot offset in UnitView so shrinking/growing the physical body cannot move the art anchor.
- [x] Through the live Editor, migrate the actual defaults from `diameter = min(spriteWorldWidth, spriteWorldHeight)` to body `diameter * .4`, hit `diameter * .55`, preserving the prior body value as foot offset. Re-read every unit's values.

## Task 2: Remove forced crowd displacement

Files: `Core/Movement/CrowdSolver.cs`, `MovementSystem.cs`, `MovementSettings.cs`, `Core/World/SpatialIndex.cs`, `WorldQuery.cs`, `Unity/Configuration/DevelopmentDefaults.cs`, `Tests/EditMode/CrowdMovementTests.cs`.

- [x] First run a regression that overlapping ground units with zero movement stay at their exact positions. Existing iterative correction must fail it.
- [x] Replace Constrain/Resolve with one nearby-candidate pass. Weight normalized separation by overlap, average it, and blend at strength .65 with voluntary movement; clamp final travel to the requested distance. Exclude player and air; never modify another entity or feed knockback through steering.
- [x] Add a caller-owned QueryCircle List overload preserving exact membership and deterministic ID order. Reuse the steering list and the movement unit list.
- [x] Replace the unused crowd iteration setting with bounded avoidance strength. Keep the terrain sweep as the final movement authority.
- [x] Verify stationary overlap, deterministic identical-center handling, movement speed bound, wall slides, attack knockback, air traversal and existing cap behavior.

## Task 3: Measure and report

Files: `Tests/Shared/RunTestRig.cs`, `Tests/PlayMode/CrowdPerformanceComparisonTests.cs`, planning/architecture/harness/handoff documents and `log.md`.

- [x] Let the fixture consume real development unit definitions and preserve new radii when overriding health.
- [x] Measure 300/500/1000 at the same .58 spacing with actual FixedUpdate and WorldPresenter, 40 warmup steps then at least 350 measured steps and 20 rendered frame intervals, bounded to 90 seconds per condition. Keep every requested unit visible and alive.
- [x] Add a separate 1000-enemy sample with player AI, four baseline weapons, contact and XP active; high fixture HP prevents population loss. Record attacks, projectiles and visible counts rather than presenting it as ordinary balance.
- [x] Run focused Core combat/crowd/cap and PlayMode view/feedback tests after recompilation. Match fresh source hashes, receipts and nonzero test counts.
- [x] Report model milliseconds per tick separately from view milliseconds and frame intervals, sample counts and limitations. Preserve cap 500, save evidence under Logs/Validation and locally commit only owned files.
