# Development workflow — Hustle and Scale

**Primary IDE:** JetBrains Rider. For Unity integration check the bundled Unity Support plugin, `com.unity.ide.rider` (only after recovering actual package state), and the Unity External Script Editor setting. Use Rider inspections and debugger, but validate Unity compilation and PlayMode separately; do not prescribe VS Code as the default.

Status: locally integrated repository policy, 2026-10-18; adapted from archive v0.3.1 to the actual working tree. Actual code, Git state, and confirmed test results remain the technical source of truth.

**Technical baseline:** Unity 6000.6.0f1, package manifests, migrated settings and asset metadata are versioned. Any further migration needs a separate task. Local installed `.agents/` skills and supplied `/references/*.zip` archives are ignored. `ProjectSettings/EditorBuildSettings.asset` enables CityGreybox; FirstDealTrade is present but disabled (checked 2026-10-21).

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

## 4. Verification contract

- Evidence tiers: **inspected only**, **static checks**, **Unity compilation**, **EditMode tests**, **PlayMode/manual tests**, and **player playtest** are distinct. Never replace one with claims about another.
- For code edits: inspect relevant call sites, run `git diff --check`, review errors; add focused automated tests for deterministic state transitions where feasible.
- For Unity-scene or UI edits: validate actual component references and player actions in the editor. Do not claim scene functionality merely because YAML parses.
- For economy/orders: verify money is not spent or paid twice; items cannot duplicate; invalid and expired trades cannot pay; state changes are consistent.
- If Unity is not available, explain the missing validation, give the minimal manual reproduction steps, and avoid labeling the task 'fully verified'.

## 5. Codex token efficiency

The canonical project rules are in [AGENTS.md](../AGENTS.md), under Efficient investigation. Keep that policy in one place rather than copying it into skills, hooks or recurring prompts.

- Prevent unnecessary context at the source: use scoped searches and relevant file ranges; preserve full build/test logs outside the prompt and report inspected results with their paths. Never treat truncation as proof that no errors or matches exist.
- At a genuine context handoff, carry the goal, approvals, production gate, confirmed findings, affected paths, unrelated changes, checks/results and next action. Revalidate affected facts after changes. Do not create automatic maps or per-turn summary files.
- The OpenCode lean-context message-transform hook is not a Codex plugin API. These rules adapt its context-minimization goal; they do not delete prior messages or reasoning. Do not edit session transcripts to simulate it, inject the full Ponytail ruleset, or install redundant hooks.
- Model, reasoning effort and compaction settings remain explicit choices; this policy does not lower them automatically. Savings require a comparable measurement, and no percentage is guaranteed.
- Use agents only when authorized and useful for independent work. Never concurrently modify the same Unity scene/prefab or `.meta` across worktrees.

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

Keep only files that reduce recurring ambiguity. Initial set: `README.md`, `AGENTS.md`, `docs/DEVELOPMENT.md`, `docs/ENGINEERING_STANDARD.md`, `.editorconfig`, `.rgignore`, plus existing `.gitignore` and `.gitattributes`. A detailed GDD and FIRST DEAL specification can be imported later **only with explicit approval and accurate current versions**; external documents are not silently presumed to live in the repository.

The documentation map is in [README](../README.md). Keep changing task status in ROADMAP, production order in DEVELOPMENT_PLAN, current city behavior in CITY_GAMEPLAY, and dated evidence in the relevant ledger. FIRST_DEAL describes the separate fixture; PLAYTEST provides the current human-observation protocol. Do not copy full results into every entry point. Mark historical prompts and observations with their scope/date, preserve asset notices, and check local links/anchors when moving sections. A source-derived behavior summary is not approval of new game rules.

Do **not** create `CONTRIBUTING.md`, ADR directories, CODEOWNERS, GitHub Actions, a custom task tracker, `.codex/skills/`, release workflows, or a universal Unity skill yet. Add them in response to a specific repeated bottleneck or delivery requirement.

## 8. Completion report

Report the exact files touched; what changed and why; Git commit hash if created; checks performed with environment/result; and what remains unverified. A local commit is a checkpoint, **not** a guarantee the game runs.


## 9. Development playground


Use this playground for focused mechanic experiments and reproducing defects. Integrated gameplay, distances and player choice are evaluated in the compact city district; completing every future feature on the playground is not a production milestone. Keep the existing scene rather than recreating it for the new plan.

Open `Assets/Scenes/DevelopmentSandbox.unity` or **Tools > Development Sandbox > Open Playground**, then enter Play Mode. The separate 260 × 220 m scene contains the live FIRST DEAL workshop/traders, a utility vehicle and driving circuit, steps/slopes/narrow gaps, a covered flashlight corridor, all 15 existing prefabs as linked display samples, placement surfaces and a streetscape scale area. The 10 m grid and seven named hierarchy groups support further editing; the creation menu refuses to overwrite an existing playground.

**F2** opens zone teleports, the clock switch and a full scene/deal reset. Passive deal time starts held; enable it to test deadlines. Repair still applies its normal four-minute cost. Normal trade, carrying and flashlight controls remain available. At the vehicle door, E enters/exits, WASD drives, Space brakes, and V switches camera motion. Driving suspends the trade interface and restores it on exit.

Catalogue objects are display references with gameplay, cameras and audio disabled; use zone 01 for working interactions. `AppleRed` and `Slot` represent the legacy inventory/UI rather than a second inventory system. Edit shared source prefabs to propagate changes, or copy authored zone objects into a game scene. The utility vehicle is also saved as `Assets/Prefabs/DevelopmentSandbox/UtilityVehicle.prefab`; assign its walker and walking camera when using it in another scene. The playground is intended for Editor use and does not alter the current build scene list; include it explicitly if a future Player build needs it.
