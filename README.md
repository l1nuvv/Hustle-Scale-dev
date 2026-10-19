# Hustle and Scale

A Unity first-person business-simulation prototype. The current playable scenario, **FIRST DEAL**, covers buying used lamps, diagnosis, repair, competing orders, delivery, seller buyback and shift outcomes. One human run is recorded under T06; further player routes and balance validation remain.

## Setup and launch

- Open the project with **Unity 6000.6.0f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. JetBrains Rider is the primary C# IDE.
- Fetch required Git LFS objects before editing. Dependency versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`; routine setup must not upgrade the Editor or packages.
- Open `Assets/Scenes/FirstDealTrade.unity` and enter Play Mode. This is the enabled global build scene. Window defaults are 1600 x 900, windowed and resizable.
- Controls: WASD, mouse, Left Shift to sprint, E to interact, Escape for pause/cursor release. Use arrows and Enter in dialogs. Introduction and offer dialogs pause time.
- Existing standalone executables require a separate rebuild to include current source changes. Clean-clone/player-build reproducibility has not been verified.

## FIRST DEAL

Start with $300 and no owned lamps. Read the offers board; the shift begins on the first exit through the passage. Buy the seller's three-lamp lot for $180, carry its crate to the workshop and dock it. Select individual lamps, place them on the bench and use TEST. Two lamps work; one blinks and switches off. A spare battery costs $50; replacement consumes it, adds 04:00 to the shift and requires a new test.

Accept A and/or B from the board and physically deliver tested working lamps to the corresponding outside counter:

| Order | Requirement | Payment | Deadline |
| --- | --- | --- | --- |
| A | Two lamps | $260 | Before 09:00 |
| B | One lamp | $125 | Before 18:00 |
| Repeat B | One lamp, after completing B | $110 | Before 18:00 |

A carried crate can deliver two lamps together; a loose held lamp delivers one. Stored, untested, faulty and already sold lamps cannot transfer. Declined, cancelled, expired and failed orders stay closed.

Seller buyback opens after the first successful A/B sale, or at 18:00 as the fallback. Bring diagnosed lamps in hands or a carried crate to the seller: tested working lamps pay $60 each, diagnosed faulty lamps $35 each. Any invalid lamp rejects the entire transfer. Repair invalidates the diagnosis.

Escape -> **Итоги и следующий шаг** shows cash, revenue, expenses, cash change, remaining stock, orders and the next opportunity. Continue trading or choose **Завершить смену**. Finishing without a primary sale requires abandonment confirmation. A finished shift freezes the session; **Начать заново** restores the initial state. Unsold goods are not automatically liquidated; cash change excludes their value.

## Scenes and verification

Earlier scenes remain integration fixtures, not duplicate releases:

| Scene | Purpose | Editor verification menu |
| --- | --- | --- |
| `SampleScene` | Original inventory and pickup regression | Included in T02 |
| `FirstDealGraybox` | Movement, collisions and world interaction | Tools > T02 > Verify Graybox |
| `FirstDealWorkshop` | Post-purchase diagnosis and repair fixture | Tools > T03 > Verify Workshop |
| `FirstDealTrade` | Complete current scenario | Tools > T04 > Verify Trade; Tools > T05 > Verify Outcomes |

Recorded verification on 2026-10-18 in Unity 6000.6.0f1: T02 **23**, T03 **51**, T04 **89**, T05 **70** checks passed during their implementation/checkpoint tasks. These historical results are Editor automation using virtual input, not a formal NUnit suite, current-run results or independent human playtests. Local evidence is under ignored `Logs/`. Rerun relevant checks after changes.

Scene construction/presentation utilities remain in `Assets/Editor/`. **Tools > T04 > Apply Playable Presentation** includes outcome setup; **Tools > T05 > Apply Outcomes** reapplies the outcome UI.

V1 environment, 2026-10-20: the trade scene has a 2m workshop passage, route markings, a workshop entrance, a roofed seller stall and distinct buyer counters with square A / round Б signs. Four original Unity prefabs and their materials live in `Assets/Prefabs/FirstDealEnvironment/`; no downloaded art was imported. World text now respects walls and sign backs. **Tools > V1 > Apply Workshop and Yard** reapplies this layout and is included in the T04 presentation utility.

Fresh Unity 6000.6.0f1 verification: **Tools > V1 > Verify Routes and Interactions** passed **72** checks for CharacterController routes, crate/loose-lamp transport, 2.5m interaction reach and A plus buyback at $415. Evidence: `Logs/Codex-V1-verification.log` and `Logs/V1-*.png`. The older T04 verifier stopped at its outdated introduction-text assertion; its complete run is not counted as passing. Art acceptance and independent player observation remain pending; see the roadmap for the limited Editor rendering measurement and remaining performance acceptance.

### T06 verification and player playtest

**Tools > T06 > Verify Invariants and Routes** runs seven explicit trade routes and 64 deterministic seeds of 256 mixed domain actions. It checks the cash ledger, item identities and capacity, diagnosis, strict active deadlines, rejection atomicity, change notifications, irreversible sales/closed orders and the frozen final state. Failures include the seed and step; evidence is written to `Logs/Codex-T06-verification.log`. Run T02, T03, T04 and T05 separately for scene/input/UI coverage. These are Editor verification harnesses, not NUnit test cases.

On 2026-10-19, Unity 6000.6.0f1 compiled the added harness with no reported errors. Fresh runs passed T02 **23**, T03 **51**, T04 **107**, T05 **85** checks and T06 **222,310 assertions** (16,384 mixed actions plus route/final-state checks). Offers, carried-crate HUD and B summary screenshots were visually inspected at 1223 x 705. An initial T02 run failed its startup focus-prompt assertion; a complete repeat passed without a gameplay change. Startup focus remains a manual follow-up, not a confirmed fixed defect. Other resolutions and a current standalone Player were not checked.

Expected route totals below exclude walking/reading time; timed deliveries must still beat their deadlines. Remaining lamps are diagnosed before buyback.

| Route | Final cash | Cash change from $300 |
| --- | --- | --- |
| A, faulty remainder sold back | $415 | +$115 |
| A + B with repair (either order) | $455 | +$155 |
| B + repeat B, faulty remainder sold back | $390 | +$90 |
| B + repeat B, repaired remainder sold back | $365 | +$65 |
| Miss both deadlines, sell all back without repair | $275 | -$25 |
| Repair, miss both deadlines, sell all back | $250 | -$50 |
| Buy lot, abandon with all lamps unsold | $120 | -$180, three lamps retained |

One manual player run reported on 2026-10-19 reached the summary at 06:03 with B and repeat B completed, no lamps remaining and $365 cash ($300 + $295 revenue - $230 expenses). The player reported no confusing step. The ledger is visible in the supplied screenshot; exact physical actions and end-shift confirmation were not independently observed. Further player routes and balance acceptance remain pending. Use the current `FirstDealTrade` scene in Play Mode, without verification scripts driving the session:

That screenshot exposed a repeated continuation sentence on the sold-out summary. The summary now keeps the sold-out next step once; the 2026-10-19 T05 rerun passed **87** checks, including the initial and sold-out summary text.

1. Give a new player only the on-screen instructions. Record time to first purchase, successful test and sale; note every request for help, wrong destination and unclear refusal. Let them choose a route and finish/restart voluntarily.
2. On fresh attempts, check A plus buyback, repaired A+B, B plus repeat and buyback, missed deadlines/fallback, and abandonment. Check the displayed cash/stock against the table. Test both a loose lamp and the carried crate; attempt an untested delivery, repeat payout and post-repair delivery before retesting.
3. Pause while carrying goods and near a deadline; confirm movement/interactions and the clock pause together. Try keyboard-only dialogs, Escape, finish confirmation and restart. Inspect text at the intended window size and a smaller window.
4. Record route, actual completion time, final ledger, confusion points, perceived time pressure and whether repair/repeat/buyback were understood. Report observed failures separately from player preferences. Editor automation cannot measure these outcomes.

Do not add another gameplay system until that playtest establishes the next bottleneck. Prioritize navigation/instruction fixes if players need help; adjust balance only from observed route times and choices. No claim about fun, independent usability or balance follows from the automated checks alone.

Repository cleanup removed the URP welcome/tutorial, unused template input actions and obsolete root URP settings, unused food demos/prefabs/meshes, and four unreferenced legacy scripts. The original apple model and scene dependencies remain. TextMesh Pro resources, shaders and license/attribution files remain intact. Local caches, IDE files, reference archives and installed agent skills are ignored rather than versioned.

V2 props, 2026-10-20: five original prefabs in `Assets/Prefabs/FirstDealProps/` replace the flashlight, crate, battery repair sample, workbench and tester visuals. Six shared materials and combined meshes keep each prefab to 2–5 renderers. **Tools > V2 > Apply Models and Materials** reapplies them and is included in the T04 presentation utility. Existing item identities, interaction colliders and diagnostic lights remain connected; the meter scale is decorative and the battery sample does not represent spare stock.

Unity 6000.6.0f1: **Tools > V2 > Verify Models and Trade** passed **110** checks including repeat application/GUIDs, carry scale, repair/retest and A+B at $455; the V1 physical-route verifier passed **72** checks after integration. Evidence: `Logs/Codex-V2-verification.log`, `Logs/Codex-V1-verification.log`, `Logs/V2-*.png`. See the roadmap for the limited before/after Editor render measurement. Owner art acceptance, independent player observation and Player performance remain pending; no Player build or archive was created.

V3 lighting and surroundings, 2026-10-20: a roof and beams enclose the workshop, two warm task lights illuminate the work area, and daylight with soft sun shadows distinguishes the yard. Wall shelves, closed storage boxes and an edge drain add restrained detail without colliders. Five shared materials live in `Assets/Prefabs/FirstDealLighting/`. **Tools > V3 > Apply Light and Surroundings** reapplies the scene dressing and is included in T04.

Unity 6000.6.0f1: **Tools > V3 > Verify Light and Surroundings** passed **46** checks; V1 routes passed **72**, and V2 models/trade passed **110** after integration. Four views were visually inspected. Evidence: `Logs/Codex-V3-verification.log`, `Logs/V3-*.png`, and the V1/V2 logs. The roadmap records a limited Editor render measurement; owner art acceptance, independent player observation and Player performance remain pending. No Player build or archive was created.

V4 characters, 2026-10-20: three original prefabs in `Assets/Prefabs/FirstDealCharacters/` replace the capsule traders. The seller has a cap/apron, A an order clipboard, and B a different arm pose. Shared materials and combined meshes use 9–10 renderers per character. Event-driven head/arm reactions read the existing session; pause, end-shift and component disable stop/reset reactions. Counter-front labels leave faces visible. Characters add no colliders or economy rules. **Tools > V4 > Apply Characters** reapplies them and is included in T04.

Unity 6000.6.0f1: **Tools > V4 > Verify Characters and Integration** passed **129** checks; V1 routes passed **72**, and V2 models/trade passed **110** after integration. Three character views were visually inspected. Evidence: `Logs/Codex-V4-verification.log`, `Logs/V4-*.png`, and the V1/V2 logs. See the roadmap for the limited capsule/character Editor render comparison. Owner art acceptance, independent player observation and Player performance remain pending. No Player build or archive was created.

## Development

- `AGENTS.md` - repository constraints and agent workflow.
- `docs/DEVELOPMENT.md` - commits, focused verification and Unity asset handling.
- `docs/ENGINEERING_STANDARD.md` - Unity/C# and gameplay architecture constraints.
- `docs/ROADMAP.md` - staged game-development plan, dependencies, acceptance criteria and effort estimates.
- `Assets/Scripts/`, `Assets/Scenes/`, `ProjectSettings/` - source, scenes and configuration.

Approved FIRST DEAL rules were supplied externally; design documents are not automatically part of this checkout. No automatic engine/package migration, new paid services or release packaging.
