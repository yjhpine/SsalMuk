# Library wall escape correction

User report: AI repeatedly approaches the upper bookcase wall, loses escape angles and dies early. User confirmed the library map. This is a correction to the approved wall-clearance and emergency-avoidance behavior, in the existing checkout.

## Diagnosis and bounded design
- Existing EvadeState considers wall clearance only when the entire continuous enemy-risk score is exactly tied. A slightly farther retreat from an enemy therefore wins even when it points into the wall.
- Breakout keeps its heading until a very short movement segment is blocked; wall comfort is also subordinate to its heading retention term.
- Chest travel always marks the player as approaching a pickup, reducing normal wall room even while the destination is far away.
- Keep actual immediate contact/charge danger ahead of comfort. Among safe alternatives, compare nearby enemy spacing together with predicted library wall room. Restore room earlier in normal movement, and relax it only for a nearby pickup/chest so existing collection remains reachable.
- Do not change monster speed/damage, map geometry, reward rules, forest movement or global performance/balance rules. No build, push or long play test.

## Steps
- [x] Reproduce symmetric upper/lower wall retreat and short continuous pursuit with existing real simulation.
- [x] Correct library escape ranking, proactive breakout re-evaluation and pickup-specific clearance.
- [x] Run targeted wall, existing safety/loot/chest/boss regression cases and a short live library check.
- [x] Record evidence and save only task-owned files in a local commit.

No new design approval is required for this bug correction; the user requested a fix and the existing contract already requires wall space without sacrificing emergency safety.

## 2026-11-04 continuation
- The initial 57 targeted cases and Static passed on source `65c9a3a4b65febcfbcb8af202a8b159e404fe6f0f55e8f5f72dcf7dbb9d25329`; they are historical evidence, not completion of the user-reported behavior.
- The subsequent live run reached 23.04 seconds at Y=27.3548, HP=60, Breakout, then Results. The fix remains incomplete until the remaining wall-following behavior is addressed. No long-survival guarantee is inferred from the short tests.
- A wall-parallel breakout can keep its direction while terrain alone prevents the blocked-fraction release threshold. Add symmetric regression cases for an opened interior route after a crowd clears, and re-evaluate wall-adjacent breakout headings on the existing planning cadence.
- The user asked the agent to open Unity and continue. The same project and version are being opened through the official CLI; no second Editor or package upgrade is needed.

## Final evidence
- A less-severe wall fixture already released correctly; the closer upper/lower cases at Y=28.1/3.9 reproduced perpetual parallel breakout with 0.651 clearance in `aa16a47d4121436d8a4dcf9f2ea9b2e3`.
- Re-evaluation at the existing 0.2-second planning cadence restores about 3.500 room and Collect in three seconds. Repeated unchanged choices preserve the stall timer.
- All 59 targeted tests plus Static passed on source `8580539ac5099a567b62d2ac7e58bf9dcfbc0d007a5bce364bfcadee3e6c0b01`. The live library run reached 28.10 seconds alive, moved from the upper side through the center to the lower side and back; final HP35 is not a survival-balance guarantee.
- Evidence and limitations are recorded in `docs/development/HANDOFF.md` and ignored `Logs/Validation/library-wall-escape-20261104/`. No full suite, long test, build or push.
