# Hustle and Scale

A Unity first-person business-simulation prototype. The current playable scenario, **FIRST DEAL**, covers buying used lamps, diagnosis, repair, competing orders, delivery, seller buyback and shift outcomes. Independent human playtests and balance validation remain T06.

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

Repository cleanup removed the URP welcome/tutorial, unused template input actions and obsolete root URP settings, unused food demos/prefabs/meshes, and four unreferenced legacy scripts. The original apple model and scene dependencies remain. TextMesh Pro resources, shaders and license/attribution files remain intact. Local caches, IDE files, reference archives and installed agent skills are ignored rather than versioned.

## Development

- `AGENTS.md` - repository constraints and agent workflow.
- `docs/DEVELOPMENT.md` - commits, focused verification and Unity asset handling.
- `docs/ENGINEERING_STANDARD.md` - Unity/C# and gameplay architecture constraints.
- `Assets/Scripts/`, `Assets/Scenes/`, `ProjectSettings/` - source, scenes and configuration.

Approved FIRST DEAL rules were supplied externally; design documents are not automatically part of this checkout. No automatic engine/package migration, new paid services or release packaging.
