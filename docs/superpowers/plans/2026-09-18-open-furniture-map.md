# Open Furniture Map and Movement Cost Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the bounded movement task, with the controller owning world generation, geometry, art integration and all live Unity validation. Track completion here; never run concurrent Unity tests or reloads.

**Goal:** Replace chunk boundary walls with open ground and sparse abandoned furniture, then reduce redundant terrain, direction and crowd computations without weakening collision or combat.

**Architecture:** Keep the deterministic 32×32 chunk occupancy grid and add immutable furniture placement metadata. Furniture visuals have baked lean/wear while their static footprint remains a simple cell rectangle. Optimize short terrain queries and broad-phase empty regions; cache monster direction decisions while moving every fixed tick; bound crowd steering work independently from exact damage queries.

**Tech Stack:** Unity 6000.4.6f1, C#, existing Core/Presentation/Unity layers, official Unity CLI/Pipeline, generated PNG sprite art.

**Spec:** Latest user approval following the open-floor/furniture proposal; existing docs/planning/GAME_DESIGN.md and docs/superpowers/specs/2026-09-16-ssalmuk-architecture-design.md, updated with this change.

## Global Constraints

- Work in C:/Users/ace21/Desktop/SsalMuk on codex/unit-foundation; one existing Unity Editor, local commits only, no push.
- General population cap stays 500; 1000 is a diagnostic condition. Retain ordinary enemies, bosses and experience. Air departure rule unchanged.
- Preserve body 80%, hurt 110%, short invulnerability, attack knockback, closest-target policy, automatic combat and continuous level-up gameplay.
- Furniture is static, impassable and indestructible; attacks and air monsters pass through. No placed gameplay units or obstacles in scene assets.
- The user's '40% leaning feeling' describes worn/tilted art, not obstacle density or an exact rotation angle. Bake the lean into sprites; use simple floor footprints.
- Keep wide traversable gaps and initial spawn clearance. Deterministic generation is independent of visit order/reward RNG and safe across negative chunk coordinates.
- No hidden attack caps, skipped damage targets, terrain penetration, monster deletion, timestep slowdown, engine/package replacement or automatic long stress/build runs.
- Stop and checkpoint at account remaining <=3%; never redeem reset credits automatically.
- Baseline 0718fe4 and source a6d5c8307a91ad962f03eb64ab59cec31d9760732e7392ebbdc6a0c03a854fc9. Preserve baseline evidence in Logs/Validation/body-hit-separation-20260917/.

## Task 1: Open generation and static furniture metadata (controller)

Files: Core/World/ChunkGenerator.cs, ChunkData.cs, MapSettings.cs; new FurniturePlacement.cs; Tests/EditMode/OpenFurnitureWorldTests.cs.

Interfaces: ChunkData keeps IsBlocked/OpeningStart/Fingerprint and its existing constructor; an optional furniture list and readonly Furniture property extend it. Placement stores kind, grid X/Y/Width/Height and a stable visual variation. Existing hand-authored test grids remain valid.

- [x] Write tests against existing behavior: every boundary cell is open for nonzero obstacle presets, valid seeds still create interior obstacles, obstacle groups are small and separate, deterministic regeneration and boss clearance remain.
- [x] Run `validate.ps1 -Mode EditMode -Filter SsalMuk.Tests.OpenFurnitureWorldTests` after recompile and observe boundary-wall failure.
- [x] Generate furniture in spaced interior slots, reserve central spawn space, mark only their simple rectangles blocked, eliminate boundary walls and random isolated wall tiles. Example invariant: `Assert.That(chunk.IsBlocked(0, y), Is.False)` for all y and neighboring chunks.
- [x] Verify new map plus existing WorldGeneration/Navigation/WorldStore tests and metadata-to-blocked-cell equality.

## Task 2: Terrain query optimization (controller)

Files: Core/World/WorldQuery.cs, ChunkData.cs; Core/Movement/CircleSweep.cs; Tests/EditMode/TerrainQueryOptimizationTests.cs, existing CircleSweepTests.

Interfaces: Keep IWorldQuery unchanged. Add internal known-safe sweep routing for WorldQuery, and a conservative blocked-region query on ChunkData. General callers still reject invalid starting positions. Short sweeps enumerate a small rectangle without a HashSet; long sweeps retain segment traversal.

- [x] Add fast empty-region and collision equivalence tests including negative chunk boundary, tiny corner contact, high-speed knockback and a large body. Use generated empty and actual single-wall worlds rather than mocks.
- [x] Preserve exact hit fraction/normal with `Assert.That(hit.Value.Fraction, Is.EqualTo(expected).Within(1e-9))` and safe endpoint checks.
- [x] Remove duplicate start checks only through a proven-safe internal call; use conservative empty-region rejection before exact tile geometry. Reuse storage and keep public invalid-input behavior.
- [x] Run CircleSweep/WorldPosition/Navigation/CrowdMovement/ContactDamage/Fireball regressions on the final source, then compare actual rendered load results.

## Task 3: Direction decision cadence and bounded crowd steering (movement worker)

Owned files: Core/Movement/MovementSystem.cs, MovementSettings.cs, CrowdSolver.cs; Core/Navigation/NavigationService.cs if necessary; Core/World/SpatialIndex.cs only for a dedicated crowd query; new Tests/EditMode/MovementOptimizationTests.cs. Do not edit controller files or DevelopmentDefaults.cs.

Interfaces: MovementSystem.Step/SetMoveIntent/AddKnockback and CrowdSolver.Steer signatures remain compatible. New MovementSettings optional parameters have conservative defaults; existing named arguments remain valid. Exact QueryCircle and damage candidate semantics remain unchanged.

- [x] Create behavioral tests for fewer repeated direction decisions with continuous motion, immediate blocked/knockback recovery, no extra speed and deterministic dense crowd behavior. Send the controller the filter for the RED run before implementing.
- [x] Cache ordinary/boss direction decisions for about 0.1 seconds with staggered refresh. Keep FSM life/knockback transitions immediate; forced input, player low-AI and air movement are not throttled. Refresh when blocked, stale route/terrain, approaching/passing a waypoint or knockback ends.
- [x] Replace all-neighbor dense steering with a bounded/aggregated spatial method with reusable storage. Limit only avoidance work; do not truncate general combat queries. Preserve stable order, no positional separation, no boosted speed, air/player exclusion, attack knockback independence.
- [x] Self-review and provide exact owned file list and targeted checks. The controller runs all live Unity checks; coordinate a source edit freeze during validation. Do not commit separately or touch scenes/settings.

## Task 4: Furniture art, runtime view and final validation (controller)

Files: Content/Art/Environment/AbandonedFurniture.png and importer metadata; Unity/Configuration/VisualCatalog.cs; Unity/Views/TerrainView.cs and WorldView.cs; standalone Editor import helper; Tests/PlayMode/FurniturePresentationTests.cs; relevant planning/harness/handoff documents.

- [x] Generate one transparent atlas for worn leaning bookshelf/desk/chair variants, inspect it, copy the original image unchanged into the project. Use Unity sprite importer APIs for slicing, Point filter, no mipmaps/compression, no physics shape generation.
- [x] Render only generated furniture metadata at runtime with an appropriate base pivot and Y sorting. Preserve existing grid collision for custom fixtures. Ground has no chunk perimeter walls.
- [x] Verify real GameStart shows generated terrain -> player -> monsters, furniture sprites are present, bounds/pivots and occupancy agree, no prefab/scene gameplay preplacement, no shadows return, and air/weapon terrain pass-through remains.
- [x] Run targeted EditMode/PlayMode tests on matching manifest/receipt/source hashes. Run the existing 300/500/1000/1000-combat comparison once on final sources; distinguish empty-map comparison from furniture-map smoke and ordinary FPS.
- [x] Review the complete change for specification and correctness, address important findings, update docs, commit only task files and confirm clean task status. Report remaining performance limits if 1000 is still not smooth.

## Execution ledger

- Baseline clean; live Unity validation owned only by controller.
- Task 1 + Task 2 share ChunkData: controller owns both. Task 3 consumes unchanged WorldQuery public interfaces. Task 4 consumes Task 1 furniture metadata. No overlapping file writes delegated.
- Existing saved project/branch is explicitly selected by the user; keep it rather than creating another Unity checkout or Editor.
- RED: open-map boundary/connected-wall failures in `e9ab6997d3c942fe8d4b9ae1fe5213ea`; direction cadence in `2ce674d91b9f4c28ba272ccdbc59475d`. The first knockback fixture accidentally exercised a pending path and was corrected to an already directly reachable target before implementation.
- Independent review found three cache boundaries: completed fallback request not consumed on early returns, flow/anchor aim passing, and a pre-displacement direction left after final knockback. All three were reproduced in `9a3e5c0f1ae24836b0987a8eeb9595cc` and fixed. Focused re-review found no remaining important issue in those changes.
- The initial furniture capture was made after moving the test player while the normal LateUpdate terrain preparation was disabled. The fixture now prepares the same neighbouring 3x3 chunks before capture; the original cropped-floor capture is not the final presentation evidence.
- Final validation runs from `Logs/Validation/open-furniture-20260918/ValidateChange.ps1` with source edits frozen. Its `verified-results.json` re-reads each new manifest/receipt/XML against one source hash. No full namespace suite, long stress run or new Windows build is included.

## Final evidence

- Source: `b5d4de2c5b3d19508ee5a2fc03f6d68525719c1cf7aba6fab6410dde96f404a8`; focused EditMode 81 + PlayMode 5 = 86 passed, checked against matching new manifests/receipts/XML.
- Runtime furniture: `36dba8a360f8452b9b3874b0d494dbbf`; final screenshot `Logs/Validation/Furniture/608e464e23874fe3be2b7f813bb87800/game-start-furniture.png` inspected after matching neighbour preparation. Sprite readback confirms all six bottom pivots, RGBA32, Point, no mipmaps or generated physics shapes.
- Population comparison: `ff9e942c800e4486ab5aa51e2c0f4a2d`. 300/500/1000 movement model mean 2.25/4.07/9.74ms vs 23.51/41.32/70.78ms before. 1000 combat mean 18.82ms vs70.74ms, but model P95 27.91ms, render interval mean222.94ms and max17 accumulated steps remain a performance limitation. Configured ordinary cap500 unchanged.
- Full evidence and same-condition checks: `Logs/Validation/open-furniture-20260918/verified-results.json`, `comparison.json`, `furniture-import-readback.json`. The original baseline reports were preserved; new results are not a long-duration or build FPS guarantee.
- Independent world/geometry/art review found no blocker. The movement review's three findings were reproduced, corrected and re-reviewed, then their six regression cases passed on final source. No further product change after validation.
