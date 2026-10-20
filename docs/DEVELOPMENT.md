# Development workflow — Hustle and Scale

**Primary IDE:** JetBrains Rider. For Unity integration check the bundled Unity Support plugin, `com.unity.ide.rider` (only after recovering actual package state), and the Unity External Script Editor setting. Use Rider inspections and debugger, but validate Unity compilation and PlayMode separately; do not prescribe VS Code as the default.

Status: locally integrated repository policy, 2026-10-18; adapted from archive v0.3.1 to the actual working tree. Actual code, Git state, and confirmed test results remain the technical source of truth.

**Current baseline, 2026-10-18:** Unity 6000.6.0f1, package manifests, migrated settings and asset metadata are versioned. Any further migration needs a separate task. Local installed `.agents/` skills and supplied `/references/*.zip` archives are ignored. FIRST DEAL is enabled in the global build scene list.

## 1. Working priority

The owner-approved production sequence and stage gates are in [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md); task history is in [ROADMAP.md](ROADMAP.md). This sequence supersedes older city-first and art-first task ordering.

1. Verify the relevant actual project state, dependency manifests and scene references; reuse confirmed findings. Unity remains 6000.6.0f1. Preserve the adopted settings; neither rollback nor further migration is authorized by this policy.
2. Reuse existing FIRST DEAL, city trade/saves, sandbox and art samples. Test uncertain actions in a focused fixture; validate the integrated loop in a compact district around the city workshop. Do not rebuild the project to replay completed stages.
3. Current next gate (2026-10-20): E4 accepted directly by the owner; E5.1 bounds are recorded in DEVELOPMENT_PLAN. After E5.2/E5.3, the owner explicitly requested E5.4, E5.5, E5.6 and E5.8 together. The bounded environment, necessary NPCs, accessible city UI and opt-in Player profile are integrated; evidence and remaining acceptance limits are in ArtDirection/VALIDATION.md. E5.7 remains a separate save-verification gate, then E5.9 human acceptance; NPC visual acceptance and the final performance budget remain with the owner. Detailed repeat-playtest observations remain unreported; automated checks do not establish measured fun or balance. Keep city rules distinct from the closed FIRST DEAL scenario.
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

- First inspect task-specific paths and use precise `rg -n`, `rg --files`, `git diff --name-only`; cap output rather than dumping large files.
- Search in `Assets/Scripts/` first for C#, then `Assets/Scenes/`, `Assets/Prefabs/`, `Assets/Settings/`, `ProjectSettings/` only when runtime integration or serialization is relevant.
- `.rgignore` removes bulky bundled TextMesh Pro resources from default ripgrep searches. To inspect excluded assets explicitly, use a scoped path search with `-u`/`--no-ignore`, or read the file directly. Never treat ignore rules as a reason to skip a necessary dependency.
- Reuse confirmed context and run relevant tests rather than the whole suite after every trivial edit. Record only material discoveries. Do not automatically generate repository maps or task summaries in every session.
- Use the narrowest relevant build and tests. Do not create ZIP archives, release packages, or standalone candidates unless the user explicitly requests packaging or a release. An older plan's packaging criterion does not override this rule; reuse existing artifacts when sufficient.
- Avoid multi-agent workflows unless tasks are truly independent. Never concurrently modify the same Unity scene/prefab or `.meta` across worktrees.
- Do not assume local skill names or `SKILL.md` availability. Use an existing, relevant skill only when its trigger and expected outputs fit. Introduce a custom skill after repeated manual work reveals a stable workflow, not proactively.
- Token usage is affected by prompts, returned output and tool use; an ignore file cannot guarantee any particular savings. Never exclude critical project files solely to reduce tokens.

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

Do **not** create `CONTRIBUTING.md`, ADR directories, CODEOWNERS, GitHub Actions, a custom task tracker, `.codex/skills/`, release workflows, or a universal Unity skill yet. Add them in response to a specific repeated bottleneck or delivery requirement.

## 8. Completion report

Report the exact files touched; what changed and why; Git commit hash if created; checks performed with environment/result; and what remains unverified. A local commit is a checkpoint, **not** a guarantee the game runs.
