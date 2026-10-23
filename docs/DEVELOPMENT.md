# Development workflow — Hustle and Scale

**Primary IDE:** JetBrains Rider. Use the Unity Support plugin and the already installed `com.unity.ide.rider`; check External Script Editor = Rider and Attach to Unity Editor & Play when debugging. Personal IDE settings are not evidence of project behavior. Regenerate Unity project files through the existing integration if needed; do not treat generated `.csproj` as the package source of truth.

Status: repository workflow, audited 2026-10-22. Actual code, Git state, and observed results remain the technical source of truth. Start from the current checkout; recovery instructions in archive v0.3.1 describe an earlier repository.

<a id="technical-baseline"></a>
## Technical baseline

Read from files on 2026-10-22; no Editor launch or compilation during this documentation audit.

| Fact | Current file evidence |
| --- | --- |
| Unity | [ProjectVersion](../ProjectSettings/ProjectVersion.txt): 6000.6.0f1, revision f7f8ed4d1e24 |
| Packages | [manifest](../Packages/manifest.json) and [lock](../Packages/packages-lock.json): URP 17.6.0, Input System 1.20.1, uGUI 2.6.0, Test Framework 1.8.0, Rider 3.0.40. Both files are tracked; no recovery/install task is pending |
| Rendering | [GraphicsSettings](../ProjectSettings/GraphicsSettings.asset) resolves to [PC_RPAsset](../Assets/Settings/PC_RPAsset.asset); quality overrides also exist. Preserve the existing pipeline |
| Input / UI | Player Settings activeInputHandler = 1; gameplay uses `Player.PlayerInput`'s runtime action map. Main trade UI is uGUI; UI Toolkit availability is not evidence of a second implemented game UI |
| Language / API | Unity-supported C# 9; local generated Assembly-CSharp.csproj agrees on LangVersion 9.0. Player Settings serialize apiCompatibilityLevel = 6 (`NET_Standard` in [Unity 6.6 reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Editor/Mono/PlayerSettings.bindings.cs)), with no per-platform API entries. Unity 6.6's Standard profile is .NET Standard 2.1; extra Framework APIs require a target check. The generated IDE target v4.7.1 is not proof of the Player API profile |
| Scenes / build | [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset): CityGreybox enabled; FirstDealTrade disabled. SampleScene, FirstDealGraybox, FirstDealWorkshop, DevelopmentSandbox and ArtDirectionTest exist as separate fixtures |
| Tests | Custom Editor menu harness in `Assets/Editor`, plus opt-in Player harness/profile. `Assets/Tests/Editor/HustleScale.Physics.Tests.asmdef` contains one physics and five UI EditMode UnityTests that enter Play Mode. Runtime predefined assembly boundaries are unchanged; harness assertion counts are not NUnit test counts |

The exact manifest may contain additional packages; presence does not prove runtime use. Previous Windows64 Player checks do not establish all supported release platforms. Local `.agents/` skills and `references/*.zip` are ignored; daily work uses versioned docs. Migration, package updates and changing Player Settings require a separate authorized task.

The subsequent [clean-checkout/build audit](BUILD_REPRODUCIBILITY_AUDIT.md) verified the pinned settings through cold import, compilation, tests and Windows Player builds; the earlier documentation audit's dirty `m_StackTraceTypes` observation no longer describes the current checkout. [Build instructions](BUILD_AND_REPRODUCIBILITY.md) record commands, package/network requirements, local-vs-remote differences and remaining acceptance limits. Do not repeat release packaging for routine tasks.

## 1. Working priority

The owner-approved production sequence and stage gates are in [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md); task history is in [ROADMAP.md](ROADMAP.md). This sequence supersedes older city-first and art-first task ordering.

1. Verify the relevant actual project state, dependency manifests and scene references; reuse confirmed findings. Unity remains 6000.6.0f1. Preserve the adopted settings; neither rollback nor further migration is authorized by this policy.
2. Reuse existing FIRST DEAL, city trade/saves, sandbox and art samples. Test uncertain actions in a focused fixture; validate the integrated loop in a compact district around the city workshop. Do not rebuild the project to replay completed stages.
3. Current gate and evidence: [ROADMAP](ROADMAP.md#current-status). E4/E5 are accepted; E6.6 awaits human acceptance after usability fixes. Current city behavior/save contract is in [CITY_GAMEPLAY](CITY_GAMEPLAY.md); use [PLAYTEST](PLAYTEST.md) for observations. Do not duplicate the changing task ledger here.
4. After loop acceptance, finish one representative vertical slice, then expand in complete playable increments. Complete the first-version experience before final content/polish and release verification. Assess saves and performance as risks arise, not only at the end.

For each task, name the question, bounded scope and completion criterion. Do not expand the whole map, finish all visuals or add unrelated mechanics while an earlier required gate remains open. New gameplay rules still need actual specification; the approved sequence does not supply missing rules.

No automatic Unity upgrade, package changes, paid integrations, network stack, city-scale content, or sweeping refactor. A task may change this priority only when explicitly authorized.

## 2. Task boundaries

- Separate investigation, implementation, and verification. Read-only tasks must not change tracked files or Git state.
- Before an implementation: examine `git status --short`, current branch, relevant source files, scene/prefab integration, existing tests, and local conventions. Identify scope, risks, and a check that can prove the change.
- Write or edit the minimum files necessary. Avoid unrelated renaming/formatting. If a missing dependency or damaged Unity serialization blocks work, report the blocker before expanding scope.
- Follow `docs/ENGINEERING_STANDARD.md` for Unity-supported C# (currently C# 9), simple domain/application/Unity/UI boundaries, concrete pattern choices, tooling and test gates. Never mistake a modern desktop .NET SDK for Unity runtime support.
- For C#, follow local naming/format conventions where consistent. Prefer focused MonoBehaviour components and plain C# domain logic; use ScriptableObject for configuration when it actually helps. Keep data, gameplay rules, and UI distinguishable.
- Do not hand-edit Unity scene/prefab YAML for broad transformations. When working outside the editor, validate GUIDs, `.meta`, serialized fields, and cross-references before presenting a change as integrated.
- Do not create a new README, ADR, plan, report, or skill for each implementation task. Update durable docs only when their facts/contracts change.

## 3. Git and commit convention

**Policy: atomic local commits after logically complete changes, not after each keystroke or saved file.** In an approved change-producing task, the agent may commit its scoped work locally unless the user explicitly asks for no commit. Read-only/review/planning requests do not imply authorization for code changes or commits. Push/merge/branch creation/history rewrite always need separate authorization.

Use Conventional Commits:

`type(scope): imperative summary`

Allowed common types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `build`, `ci`, `perf`. Use an optional lowercase scope such as `inventory`, `orders`, `player`, `scene`, `unity`, `repo`. Subject in English, imperative, preferably <=72 characters, without a trailing period. The body is optional; explain non-obvious *why* and behavior changes. Mark breaking changes explicitly only when applicable.

Examples:

- `fix(inventory): preserve item instance identities`
- `feat(orders): validate checked lamps before payout`
- `test(economy): cover repeated trade settlement`
- `chore(repo): allow Unity package manifests in Git`
- `docs: document repository development workflow`

One feature/fix with its relevant tests and `.meta` may be one commit. Do not split a single coherent change into dozens of commits, bundle unrelated changes, or commit broken intermediate states just to increase commit count.

Before committing: review `git diff --check`, `git diff --stat`, diff content, and `git status --short`; stage explicit files (`git add path1 path2`, not blind `git add -A`); record which checks ran and their outcomes. Preserve another developer's existing staged and unstaged changes in their original state. Do not use force resets or destructive cleanups. During an existing rebase, do not stage, commit, continue or abort it without explicit authorization for that operation.

The versioned formatting hook is `.githooks/pre-commit`. Enable it locally with `git config core.hooksPath .githooks` (requires a .NET SDK and Git Bash). On commit it formats staged C# files using `dotnet format whitespace` and `.editorconfig`, trims trailing spaces in staged `.meta` files, and stops on errors or partially staged source files. Run `bash .githooks/pre-commit --all` to format all tracked C# and `.meta` files without staging them. Folder mode avoids restoring Unity-generated IDE projects; the hook does not apply analyzer fixes or change serialized identifiers.

<a id="verification-levels"></a>
## 4. Verification contract

Select the narrowest check that covers the changed behavior. Record command/menu, commit plus dirty paths, Unity version/mode, result and full local log path. Assertion counts in a harness are not counts of NUnit tests, unique routes or human participants.

| Change | Required applicable evidence |
| --- | --- |
| Documentation only | Source/decision comparison, relative links and anchors, focused diff review, `git diff --check`, docs-only staged paths. No Unity build or gameplay rerun solely for Markdown |
| Deterministic C# domain | Unity compilation and focused existing harness for state transitions/atomic rejections; use EditMode/Test Runner only if a real test suite exists or is added for the change |
| Unity integration | Compilation, actual scene/prefab/GUID references, relevant Play Mode action/input/lifecycle checks |
| Gameplay / save behavior | Old and changed routes, refusals and soft-locks, pause/deadline, identity/payment, save/load when affected; human usability/acceptance separately where the gate requires it |
| Build / release / performance | Explicitly selected Player build/profile on recorded target/environment, relevant launch/full-route/save checks. ZIP/release packaging only when directly requested |

### Existing verification entry points

Inspect the harness prerequisites before running: some enter/leave Play Mode, open fixtures or rebuild presentation. Preserve unsaved scene work; use the harness's isolated save paths. Availability below was checked in source, not executed by the documentation audit.

| Scope | Menu / source |
| --- | --- |
| FIRST DEAL identity/settlement/routes | `Tools/T06/Verify Invariants and Routes` — [T06Verification](../Assets/Editor/T06Verification.cs); T03 workshop, T05 outcomes for their narrower concerns |
| Physics / collision / current-camera E / placement | `Tools/Physics/Verify Collision and Interaction`, or Test Runner → EditMode → `PhysicsInteractionTests`. Same shared checks in opt-in Development Player phase `physics`; commands, limits and evidence: [physics audit](PHYSICS_COLLISION_INTERACTION_AUDIT.md) |
| City UI / modal input / vehicle transitions / diagnostic sizes | Test Runner → EditMode → `UIInteractionTests` — five tests enter actual Play Mode with isolated saves. [UI audit](UI_INTERACTION_AUDIT.md) records RED/GREEN, screen inventory, screenshots, baseline Player limits and the separate T03 harness issue |
| Playtest 23.10 / individual purchases / inventory / NPC input / tooltips | `Tools/Feedback/Verify Inventory and Partial Purchases`, `Tools/Feedback/Verify UI and Input In Play Mode`, `Tools/Feedback/Verify Closed Lamp Exit` — [PlaytestFeedbackVerification](../Assets/Editor/PlaytestFeedbackVerification.cs). Play Mode: `Tools/Feedback/Enter Isolated Play Mode` в сохранённой CityGreybox; используется только Temp/M5Feedback. Новые пути заменяют старые предположения S3/E57 о покупке тройки и меню ящика |
| City money/order/physical transfer | `Tools/S3/Verify City Trading in Play Mode` — [S3Verification](../Assets/Editor/S3Verification.cs) |
| Two lots / RNG / domain refusals | `Tools/E6/Verify Restock and RNG` — [E6Verification](../Assets/Editor/E6Verification.cs); separate `Tools/E6/Verify Second Trading Cycle in Play Mode` for integration |
| City soldering / saves v5 | `Tools/M5/Verify Domain and Saves` — [M5SolderingVerification](../Assets/Editor/M5SolderingVerification.cs) |
| City soldering input / interruptions | `Tools/M5/Verify City Input In Play Mode`, `Tools/M5/Verify City Interruptions In Play Mode` with `HustleScale.S2SaveOverride` containing `M5`; [conditions and results](M4_SOLDERING_INTEGRATION.md#m5--реализация-и-проверки-22102026). Set the isolated override before entering Play Mode |
| City sharpening / v7 saves / input | `Tools/M7/Verify Domain and Saves`; `Tools/M7/Enter Isolated City Play Mode`, затем `Tools/M7/Verify City Input In Play Mode` — [M7SharpeningVerification](../Assets/Editor/M7SharpeningVerification.cs); [результаты и ограничения](M7_SHARPENING_INTEGRATION.md). Изоляция `Temp/M7`, без Player/архивов |
| City save data/store | `Tools/S2/Verify City Save and Load` — [S2Verification](../Assets/Editor/S2Verification.cs) |
| World persistence / Q / E,T,R | `Tools/E5/Verify Slice Persistence` — [E57PersistenceVerification](../Assets/Editor/E57PersistenceVerification.cs) |
| City prefab placement / rotated local anchors / legacy scene binding | `Tools/City/Validate Scene Bindings`; in Play Mode with `HustleScale.S2SaveOverride` under `Temp/CityPlacement...`: `Tools/City/Verify Prefab Placement In Play Mode` and `Tools/City/Verify Legacy Saves With Prefabs In Play Mode` — [CityPlacementVerification](../Assets/Editor/CityPlacementVerification.cs). Uses real adapters and restores temporary transforms; never run against the player save |
| Vehicle cargo | `Tools/City/Verify Vehicle Cargo` — [CityCargoVerification](../Assets/Editor/CityCargoVerification.cs) |
| FIRST DEAL focus/UI sizes | `Tools/U3/Verify Menus Focus and Window Sizes` — [U3Verification](../Assets/Editor/U3Verification.cs); does not establish city UI/gamepad coverage |

For duplicate-sale changes, test same-ID repeats, duplicate IDs in one group, mixed valid/invalid group, sold or distant goods, closed order and exact deadline; money/items/order must remain unchanged on rejection. For diagnosis UI, test unknown/working/fault/after-repair/retested states, visible refusal and focus, with the appropriate scene adapter. For a new product, specify and test new data and old-save compatibility before expanding the fixed three-slot model.

State reliability audit (22.10.2026): [results, limits and regression commands](GAME_STATE_RELIABILITY_AUDIT.md). Additional focused menus: `Tools/T04/Verify Trade Domain`, `Tools/Quality/Verify City State Sequences` and `Tools/Quality/Verify Pickup Integrity in Play Mode`; the last uses an isolated empty scene and restores the original saved scene.

Windows Player startup/lifecycle audit (22.10.2026): [builds, timings, Player harness commands and acceptance limits](PLAYER_STARTUP_LOADING_LIFECYCLE_AUDIT.md). The opt-in city/fixture harness and startup observer use isolated saves; production starts in CityGreybox, not a main-menu-to-FIRST-DEAL flow.

Historical results are in [ROADMAP](ROADMAP.md) and the dated ledgers; `Logs/`/`Temp/` may be absent or overwritten locally. A previous PASS cannot establish a new run. The known old T04 intro-text assertion failure is retained in [FIRST DEAL history](archive/DEVELOPMENT_HISTORY.md#first-deal--записи-проверок-1820102026), not silently reclassified as a passing suite.

- Evidence tiers: **inspected only**, **static checks**, **Unity compilation**, **EditMode tests**, **PlayMode/manual tests**, and **player playtest** are distinct. Never replace one with claims about another.
- For code edits: inspect relevant call sites, run `git diff --check`, review errors; add focused automated tests for deterministic state transitions where feasible.
- For Unity-scene or UI edits: validate actual component references and player actions in the editor. Do not claim scene functionality merely because YAML parses.
- For economy/orders: verify money is not spent or paid twice; items cannot duplicate; invalid and expired trades cannot pay; state changes are consistent.
- If Unity is not available, explain the missing validation, give the minimal manual reproduction steps, and avoid labeling the task 'fully verified'.

## 5. Codex token efficiency

The canonical rules and task routes are in [AGENTS.md](../AGENTS.md), under Efficient investigation and Task routes. Keep them in one place. The OpenCode lean-context hook is not a Codex plugin API; this repository does not install a message-transform workaround. Use agents only when authorized and useful; never concurrently modify the same Unity scene/prefab or `.meta` across worktrees.

## 6. Ignore and metadata rules

| File | Responsibility | Rules |
| --- | --- | --- |
| `.gitignore` | Stops Git from tracking local/generated files | Must **not** exclude `Packages/manifest.json`, `Packages/packages-lock.json`, `Assets/**.meta`, scenes, prefabs, ProjectSettings |
| `.gitattributes` | Text/LFS handling, Unity YAML merge driver | Existing file uses Git LFS for binary assets. Do not replace or rewrite without checking local Git LFS and any custom UnityYAMLMerge driver. |
| `.rgignore` | Hides low-value files from ordinary ripgrep searches | Has no effect on Git tracking and is not a Codex access restriction |
| `.aiignore` | Tool-dependent convention, not a universal standard | Do not assume Codex reads or enforces it; do not use it as a security boundary |
| `.editorconfig` | Basic whitespace conventions | Must not trigger mass reformatting of serialized Unity files |
| `AGENTS.md` | Durable constraints and agent workflow | Keep concise. Do not paste full GDD, complete architecture, or repeating checklists |
| `README.md` | Entry point, factual state, setup and navigation | Clearly distinguish verified working features from planned ones |

**Verified baseline (2026-10-18):** `Packages/manifest.json` and `Packages/packages-lock.json` are tracked. Root exceptions override the generic package ignore rule. Preserve both files and verify their actual contents when investigating dependencies.

The current `.gitattributes` uses Git LFS for many media/binary file types and `merge=unityyamlmerge` for serialized Unity assets. Review actual Git LFS initialization before adding content. A declared merge driver is not proof the driver is configured on the machine.

## 7. Documentation and skills policy

Keep only files that reduce recurring ambiguity. [GDD](GDD.md) now owns the consolidated concept/PX and [FIRST_DEAL](FIRST_DEAL.md) owns the approved fixture contract imported during the owner-requested documentation audit. [ARCHITECTURE](ARCHITECTURE.md) describes actual state ownership. Local archive copies are provenance, not competing daily specifications. Later external requirements still need an identifiable owner decision and a current version before import.

The documentation map is in [README](../README.md). Keep changing task status in ROADMAP, production order in DEVELOPMENT_PLAN, current city behavior in CITY_GAMEPLAY, and dated evidence in the relevant ledger. FIRST_DEAL describes the separate fixture; PLAYTEST provides the current human-observation protocol. Do not copy full results into every entry point. Mark historical prompts and observations with their scope/date, preserve asset notices, and check local links/anchors when moving sections. A source-derived behavior summary is not approval of new game rules.

Do **not** create `CONTRIBUTING.md`, ADR directories, CODEOWNERS, GitHub Actions, a custom task tracker, `.codex/skills/`, release workflows, or a universal Unity skill yet. Add them in response to a specific repeated bottleneck or delivery requirement.

## 8. Completion report

Report the exact files touched; what changed and why; Git commit hash if created; checks performed with environment/result; and what remains unverified. A local commit is a checkpoint, **not** a guarantee the game runs.


## 9. Development playground


Use this playground for focused mechanic experiments and reproducing defects. Integrated gameplay, distances and player choice are evaluated in the compact city district; completing every future feature on the playground is not a production milestone. Keep the existing scene rather than recreating it for the new plan.

Open `Assets/Scenes/DevelopmentSandbox.unity` or **Tools > Development Sandbox > Open Playground**, then enter Play Mode. The separate 260 × 220 m scene contains the live FIRST DEAL workshop/traders, a utility vehicle and driving circuit, steps/slopes/narrow gaps, a covered flashlight corridor, all 15 existing prefabs as linked display samples, placement surfaces and a streetscape scale area. The 10 m grid and seven named hierarchy groups support further editing; the creation menu refuses to overwrite an existing playground.

**F2** opens zone teleports, the clock switch and a full scene/deal reset. Passive deal time starts held; enable it to test deadlines. Repair still applies its normal four-minute cost. Normal trade, carrying and flashlight controls remain available. At the vehicle door, E enters/exits, WASD drives, Space brakes, and V switches camera motion. Driving suspends the trade interface and restores it on exit.

Catalogue objects are display references with gameplay, cameras and audio disabled; use zone 01 for working interactions. `AppleRed` and `Slot` represent the legacy inventory/UI rather than a second inventory system. Edit shared source prefabs to propagate changes, or copy authored zone objects into a game scene. The utility vehicle is also saved as `Assets/Prefabs/DevelopmentSandbox/UtilityVehicle.prefab`; assign its walker and walking camera when using it in another scene. The playground is intended for Editor use and does not alter the current build scene list; include it explicitly if a future Player build needs it.
