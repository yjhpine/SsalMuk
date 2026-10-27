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
- [x] User chose the temporary existing-upgrade offer: three candidates, one selection, then explicitly resumed after the pause.
- [x] Implement deterministic persisted chest state, reach/open once, target guidance, and chest travel with nearby XP detours per the latest correction.
- [x] Add reachability, emergency interruption, revisit/reset, and reward/pause tests appropriate to that decision.
- [x] Show visible chests and one current-target edge arrow without per-chest simulation objects.

## Finish
- [x] Review and validate the completed library changes, inspect the runtime preview, and update handoff.
- [x] Save the completed library portion in a local commit.
- [x] Review and validate chest exploration, inspect actual two-map chest/arrow/reward pixels, and update handoff for the local chest commit.

## Ledger
- Chest completion: relevant Core/reward/AI EditMode 55 cases passed in the preceding source phase. After the final rendering fixes and LateUpdate-aware UI observation, current-source chest PlayMode 2 (`1c1f1d3e4c964946bf379d3a9455fa30`), MapFeature PlayMode 3 (`ca7a45a7bf724728825e3dc7005917cb`) and Static (`015dd233c0aa4bab98e5ef06fda5df70`) passed. Hash/receipt/XML comparisons and actual screenshot folders are in `Logs/Validation/chest-exploration-20261027/verified-results.json`. No full-suite, long performance, build or push was performed. Editor returned to clean stopped MainMenu.
- The first pixel-successful run (`ee37676f4edc4a5c9f01b24a1fe12b11`) observed reward UI before AppRoot.LateUpdate; the test now waits through that phase instead of asserting too early. Reward runtime behavior was unchanged by this test correction.
- Review fix pass: independent reviewer found high-speed waypoint oscillation. `FastPlayerNavigatesAroundFurnitureWithoutWaypointOscillation` reproduced it at 24 units/sec (RED `6fd6ae0c2e6642aca5e442057fb4f200`); arrival input now caps displacement at the remaining waypoint/chest/XP approach distance.
- Visual diagnosis: active views alone passed while chest and arrow pixels were absent. Normalizing triangle winding did not fix it (the actual URP shader has Cull Off); pixel checks remained RED (`8b1af953f9eb423a9d3366d42436ef83`, diagnostic `77a45e3614a2489098e5ad81590ba062`). The custom Graphic lacked CanvasRenderer, and the MeshRenderer's sprite shader needed explicit per-renderer tint/flip defaults. Live sorting/property-block isolation produced 0 chest pixels without the block and 4,169 with it at the same sorting order. Add RequireComponent(CanvasRenderer) and the explicit material property block, then verify actual images again. Diagnostic captures: `Logs/Validation/ChestExploration/render-probe*.png`.
- Earlier successful stages: initial persistence/reward EditMode 5 (`f3767ea6c9724a2a82e3d60e502eea27`), movement/manual choice 8 (`1559700df0bd41e68449c662d14eece6`), user-corrected nearby XP behavior 9 (`a66a8d9482a34a13911c02cfce407dcd`), two-map PlayMode 2 (`4affaa314b3d4bb992690ea049d9e610`). These precede the final visual/high-speed fixes and are not final-source verification.
- Unity connection timed out during recompilation. User refreshed the Editor; its Pipeline server returned on port 7801 and ready/compile-completed were verified. No second Editor, package changes, forced restart or scene overwrite was used.
- User steering supersedes strict chest-first movement: gather nearby XP while traveling toward the chest. RED `7b62a61258f04b95a889f762f19174ff` proves the old direct-chest heading ignored a nearby side orb. The local detour uses at most pickup radius + 2 (capped at 6), clear terrain, positive route progress, and 0.2-second selection; emergency checks still run on the resulting intent.
- Resumed by explicit user instruction from 46adc48. Task 3 in progress. Ruling: one random reachable chest per neighboring playable chunk, opened state retained independently of terrain cache; chest choices use the existing eligible reward pool but a separate counter and source flag, never increasing level/XP. Manual chest selection is the conservative temporary behavior since chest AUTO has not been requested. Active level-up offers remain stable, combat continues, and ESC retains its existing pause behavior.
- Ruling: enable chest generation in the real RunScope and explicitly in chest tests, leaving unrelated isolated simulation fixtures unchanged. A single player seeker uses bounded local candidates and existing budgeted navigation; no monster AI or per-chest Update/physics is introduced.
- 2026-10-27 resume point: baseline ff75572. Reward clarification answered: existing upgrades, choose one of three. Read-only code inspection started, then the user explicitly paused to continue from home. No chest code/tests or Unity operations began. No active goal. Continue Task 3 only after a new resume instruction; do not treat the old unanswered-reward entries below as current.
- Baseline 1fb667c, clean tracked files; user recovery scenes 0 (3)/0 (4), ProfilerCaptures untouched.
- The request authorizes map/presentation/AI changes; reward-dependent work waits for the pending user clarification. No repeated design approval required for already specified work.
- Initial failures: 9 rather than 3 initial chunks, 21 furnishings across 20 chunks, no inward wall steering (`8104f0b890934013b01cef98a32200b7`). The outside-sweep fixture was corrected to expect the established invalid-start exception.
- Independent review found wall comfort blocked near-wall powerups (RED `01766b081c3c466182c0ef548f534f04`) and could override safer Evade/Breakout directions (two RED cases `7e1aaf3a02b440748e5b872a010287d6`). Fixed pickup-specific clearance and made wall comfort secondary to the existing emergency score. LibraryExploration 11 passed after explicit recompilation (`d802adffdb6a4a3db90fa6a68cf5b50f`).
- Task 3 remains pending a user answer; no empty/rewardless chest or arbitrary item effects are shipped as completed gameplay.
- Final source validation: LibraryExploration EditMode 11, LowAi EditMode 18, MapFeature PlayMode 3, and Static passed. Earlier map/navigation EditMode 5+5 belong to the preceding source phase. Source/receipt/XML comparisons and preview details are recorded in `Logs/Validation/library-revision-20261027/verified-results.json`.
- Runtime library cached only Y=0 chunks (5 horizontal chunks at the observed point). The screenshot shows continuous book spines and increased furniture. Player repositioning was only for framing. Returned to stopped MainMenu with no scene changes. The Pipeline start-eval timeout is retained as a tool error; the requested scene start was verified successful before continuing.
