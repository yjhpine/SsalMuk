# Library boundaries and chest exploration implementation plan

> Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Remove non-playable library chunks, improve library presentation/movement, and connect chest exploration after the reward decision.
**Architecture:** Keep logical boundary queries in WorldStore/WorldQuery, deterministic placement in generators, batched drawing in TerrainView, and single-player movement preferences in AiContext. Chest state belongs to the run, separate from cached terrain and view pooling.
**Tech Stack:** Unity 6000.4.6f1, C#, existing uGUI and validation harness.
**Spec:** ../specs/2026-10-27-library-chests-design.md

## Constraints / review focus
- Existing checkout/branch only, preserve recovery/profiler files, no package changes/build/push/long test.
- Ground units cannot cross library bounds; air behavior and persistent units/XP remain unchanged.
- Out-of-domain navigation and sweep queries must not allocate/cache terrain.
- More furniture must leave boss-width paths and a safe start.
- Emergency avoidance must override chest attraction and wall comfort; unreachable chests cannot stall forever.
- Opened chests cannot regenerate through terrain cache eviction. Pauses and deaths cannot grant duplicate rewards.

## Task 1: Library domain and furniture
- [x] Add failing bounds/cache, initial 3-chunk loading, furniture density/corridor tests to Tests/EditMode/LibraryExplorationTests.cs.
- [x] Add the map-domain query to Core/World/WorldStore.cs; adapt WorldQuery and InitialRunBuilder; avoid out-of-domain route expansion.
- [x] Increase deterministic furniture slots in LibraryChunkGenerator and preserve movement lanes.
- [x] Recompile explicitly and run the targeted EditMode tests.

## Task 2: Library steering and visual shelves
- [x] Add wall comfort/escape regression cases before changing AiContext/LowAiController/BreakoutState.
- [x] Render the shelf wall in one batched mesh in TerrainView, keeping collision and darkness independent.
- [x] Run targeted AI/map tests and inspect a short runtime library preview.

## Task 3: Chest exploration
- [x] User chose the temporary existing-upgrade offer: three candidates, one selection. Work was then paused by the user before code changes; await explicit resume.
- [ ] Record the user's reward decision, then implement deterministic persisted chest state, reach/open once, target guidance, and chest-first AI.
- [ ] Add reachability, emergency interruption, revisit/reset, and reward/pause tests appropriate to that decision.
- [ ] Show visible chests and one current-target edge arrow without per-chest simulation objects.

## Finish
- [x] Review and validate the completed library changes, inspect the runtime preview, and update handoff.
- [x] Save the completed library portion in a local commit; chest completion remains separate and pending the reward answer.

## Ledger
- 2026-10-27 resume point: baseline ff75572. Reward clarification answered: existing upgrades, choose one of three. Read-only code inspection started, then the user explicitly paused to continue from home. No chest code/tests or Unity operations began. No active goal. Continue Task 3 only after a new resume instruction; do not treat the old unanswered-reward entries below as current.
- Baseline 1fb667c, clean tracked files; user recovery scenes 0 (3)/0 (4), ProfilerCaptures untouched.
- The request authorizes map/presentation/AI changes; reward-dependent work waits for the pending user clarification. No repeated design approval required for already specified work.
- Initial failures: 9 rather than 3 initial chunks, 21 furnishings across 20 chunks, no inward wall steering (`8104f0b890934013b01cef98a32200b7`). The outside-sweep fixture was corrected to expect the established invalid-start exception.
- Independent review found wall comfort blocked near-wall powerups (RED `01766b081c3c466182c0ef548f534f04`) and could override safer Evade/Breakout directions (two RED cases `7e1aaf3a02b440748e5b872a010287d6`). Fixed pickup-specific clearance and made wall comfort secondary to the existing emergency score. LibraryExploration 11 passed after explicit recompilation (`d802adffdb6a4a3db90fa6a68cf5b50f`).
- Task 3 remains pending a user answer; no empty/rewardless chest or arbitrary item effects are shipped as completed gameplay.
- Final source validation: LibraryExploration EditMode 11, LowAi EditMode 18, MapFeature PlayMode 3, and Static passed. Earlier map/navigation EditMode 5+5 belong to the preceding source phase. Source/receipt/XML comparisons and preview details are recorded in `Logs/Validation/library-revision-20261027/verified-results.json`.
- Runtime library cached only Y=0 chunks (5 horizontal chunks at the observed point). The screenshot shows continuous book spines and increased furniture. Player repositioning was only for framing. Returned to stopped MainMenu with no scene changes. The Pipeline start-eval timeout is retained as a tool error; the requested scene start was verified successful before continuing.
