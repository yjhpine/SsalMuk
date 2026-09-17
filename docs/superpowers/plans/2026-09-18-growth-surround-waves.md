# Growth and surrounding waves implementation plan

> Execution: implement in the user's selected SsalMuk checkout with the existing Unity Editor. Run the focused test cycle sequentially; preserve unrelated work and commit locally.

**Goal:** Correct one-at-a-time alternating weapon growth, cap repeat upgrades at level 2, add adaptive two-minute surrounding waves, and zoom the camera in.

**Approved behavior:** The user explicitly requested these changes and selected escalation based on kill speed and remaining enemies. Copies are `1+n`, ordered center, left 15°, right 15°, left 30°, right 30°. All four weapons share this layout. Repeat level 2 means three strikes per burst; capped choices disappear. Existing ordinary cap 500, boss interval 300 seconds, air lifecycle, experience preservation, and other unlimited upgrades remain.

**Initial tunable wave values:** First wave at 120 seconds; repeat every 120 seconds. Stage 1 requests 80 normal enemies around eight interleaved sectors outside the current camera. Each harder stage adds 20 requests, 20% baseline health and 10% baseline damage to that wave, composed with the existing time difficulty. Escalate by one stage only if at least 80% of the prior wave were killed in its first 45 seconds and the current ordinary population is at most 25% of that wave's spawned count. No spawn or no reliable sample does not escalate. The wave shares the ordinary cap and creation budget, does not bank excess slots, and expires blocked reservations after 5 seconds. Keep existing enemies. Camera orthographic size 10→8 is the first zoom setting.

## Tasks

- [x] Growth: add failing per-level layout, capped-upgrade, candidate exclusion and stale-choice tests; then update StatCalculator, WeaponState, GrowthService, OfferGenerator, RewardService, and the level-up wording. Adapt existing fixtures that assumed two copies or more than two repeat upgrades.
- [x] Waves: add boundary, all-sector spawn, adaptive pressure, persistence/cap/budget and restart tests; implement a focused wave policy object and connect it to SpawnDirector/SpawnTicket. Expose initial tuning values through DevelopmentDefaults and update the actual asset using Unity serialization.
- [x] Camera/live wiring: use the configured size in AppRoot; verify actual GameStart camera, next-boundary wave and offscreen placement without waiting several real minutes.
- [x] Finish: run appropriate growth/reward/spawn/combat and live integration fixtures with frozen source; verify manifest, receipt, nonzero XML counts and current source hash. Update planning/harness/handoff, check the diff, and commit only these changes. Do not restart full suites, long stress tests, builds, or remote pushes.

Verification: 70 EditMode and 5 PlayMode tests passed on source `f3db5bdfc2cf39e54380242924581d26c27549093a20b6a3b33ebe550da9956f`. Evidence: `Logs/Validation/growth-waves-20260918/verified-results.json`. The accelerated-clock live fixtures establish wiring, not survival or performance over several minutes. The adapted HighGrowth stress scenario was not executed.
