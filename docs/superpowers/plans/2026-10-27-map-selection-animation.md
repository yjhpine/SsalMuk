# Map Selection and Player Animation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Add playable forest/library selection and preserve the single-sprite walk through FSM-driven animation clips.
**Architecture:** Retain deterministic chunk queries and session ownership. Add themed generators and immutable decoration/clearing data; the view batches grass. The player view owns manual animation evaluation, independent of logical movement and hit feedback.
**Tech Stack:** Unity 6000.4.6f1, C#, uGUI, Animator, existing validation harness.
**Spec:** ../specs/2026-10-27-map-selection-animation.md

## Global Constraints
- Preserve all user-owned untracked recovery/profiler files. Work in the existing SsalMuk checkout and branch.
- No package/Editor upgrade, new build, long stress run, push, chest implementation, or monster animation.
- Preserve pause, runtime creation order, combat, population limits and persistent entities.

## Review Focus
- Negative/distant chunks and recreation must reproduce terrain and decorations.
- Forest start/clearing connections must accommodate the largest body.
- Library walls must block diagonal/swept movement; ground spawns/waves must not wait forever at a forbidden side.
- Map selection must survive restart and not change an active run.
- Player animation must freeze with game time and reset before a pooled view becomes a monster.

## Task 1: Deterministic map models and generation
Files: Core/World/{MapKind,ThemedChunkGenerator,ForestChunkGenerator,LibraryChunkGenerator,ChunkData}.cs and Tests/EditMode/MapSelectionTests.cs.
- [x] Add failing behavioral tests for clearings/obstacles/grass determinism, traversable cross-chunk corridors and library walls.
- [x] Add forest clearing/obstacle/decoration generation and library strip generation behind IChunkGenerator.
- [x] Run targeted EditMode tests and preserve existing terrain/navigation fixtures.

## Task 2: Session/menu/terrain integration
Files: RunCoordinator, RunModel, RunScope, AppRoot, IMainMenuView, MainMenuPresenter, MainMenuView, TerrainView, VisualCatalog, SpawnDirector; Editor content importer; map presentation tests.
- [x] Add failing menu-to-world/restart and library spawn regression tests.
- [x] Thread selected MapKind through construction; add two menu cards without changing existing controls.
- [x] Import static forest art, render themed floors/walls and batch non-colliding grass.
- [x] Constrain library ground spawn proposals to the interior while preserving off-screen spawn rules.
- [x] Verify both maps through real runtime creation, movement and restart.

## Task 3: Player animation clips
Files: UnitView, PlayerLocomotionAnimation, Editor/Content/PlayerAnimationBuilder, Content/Animations, VisualCatalog, Tests/PlayMode/PlayerAnimationTests.
- [x] Add failing real-view tests for an Animator, moving/idle pose, pause and reuse.
- [x] Generate Idle/Walk clips and controller, evaluate from game-frame delta, remove concurrent procedural ownership of player walk pose.
- [x] Keep hit feedback and logical movement/collision independent; no Animator on monster-only views.
- [x] Run targeted animation/feedback/menu/map checks plus Static; inspect runtime screenshots and console.

## Task 4: Finish
- [x] Review changes, update design/handoff with fresh evidence, commit only task-owned files locally.

## Execution ledger
- Baseline: 43ff729; branch codex/unit-foundation; only existing recovery scene and ProfilerCaptures untracked.
- Ruling: execute in this checkout without another approval because user explicitly approved the proposed maps and existing animation style. No new worktree because the project requires this path and one Editor.
- Initial CLI found no Editor (only CLI helper processes); launched the exact configured Editor for this project after checking its lock.
- Red evidence: missing generators `7ac72940ce564a4daec110edbae87fd3`; missing menu/Animator `ede47eb708f7489f9bf7bfce165517eb`.
- Independent read-only review found one P2: initial library enemies still used the forest ring. Added a failing off-screen spawn case (`312087b04e5b4647ad88ad086135f708`), then moved initial library proposals to alternating left/right sides inside the walls. Traversal fixtures use the catalog boss radius 1.125.
- Final recompiled map tests: EditMode 5 passed (`38c28999ff0d44b8837ca5681a484a20`); real-view/map/restart PlayMode 3 passed (`ed6a14d71ce648bfb8d05d5cd763df4c`).
- Related checks before final menu text-height adjustment: feedback/pool 3, monster presentation 2, session lifecycle 3 passed. The old monster test expected boss knockback during telegraph; corrected that expectation to the already-approved superarmor contract without changing boss behavior.
- Visually inspected the forest atlas in the actual scene, main-menu layout, and library lower wall/furniture. Library runtime advanced from 0 to 7.70 to 14.82 game seconds; only for framing, moved the player to a confirmed free location near the wall. Stopped this preview and restored edit mode. CLI game capture omits overlay HUD; separate menu/settings captures include UI.
- Visual review found clipped title/subtitle text in the compressed menu layout; enlarged their text rectangles. An external text-height edit had not yet been compiled despite receipt source hashing, so explicitly recompiled and verified the live height/vertices before repeating the affected tests. Final menu/settings 3 passed (`43598e2d840641229802fdb3a14b48b2`); Static passed (`e25c14c0056e48d991e6291e19899e67`). Final screenshot `MenuSettings/6ca84b91cdbc4c14807849bbc261ab60/main-menu.png` includes the subtitle correctly.
- Final evidence and limitations are recorded in `docs/development/HANDOFF.md`. No new build, push, full suite, or long performance run; recovery scenes and profiler captures remain untracked and untouched.
