# Hustle and Scale

An early Unity first-person business-simulation prototype. Intended gameplay centers on finding deals, assessing used goods, making trade-offs between time, stock, and money, and gradually building a business. The concept is independent of its genre references; gameplay is not yet validated.

## Current repository state

Verified local working tree on 2026-10-18, Unity **6000.6.0f1**:

- T01 technical verification passed in the existing Editor: scripts compiled, enabled `SampleScene` and all project prefabs were checked for missing scripts and broken serialized object references. A temporary Editor verifier ran Play Mode for 120 Editor updates, checked the active player and inventory slots, opened/closed inventory, and picked up a world item while checking its quantity. Final log marker: `T01 RESULT PASS` in local `Logs/Codex-T01-final-editor.log`; verifier source retained in local `Logs/T01VerificationTemp.cs` outside Assets.
- Restored 27 Git LFS pointer files from the existing local object cache (meshes, sprites and font); no remote download was needed. `Male_Casual.fbx` could not produce a valid Humanoid avatar (missing LeftFoot mapping). Its importer now uses Generic; prototype movement is script driven and has no animation controller assigned. Unity also regenerated apple sprite slicing metadata after restoring the image. GUIDs and scene/prefab references were preserved.
- The earlier rebase is no longer active; current branch is `codex/recover-prototype`, with pre-existing staged, unstaged and untracked work. Recovery preserves that work. The earlier policy documents' rebase observations describe their earlier inspection.
- Existing local package manifests agree on all 50 direct dependencies. Unity resolved 64 packages: 52 built-in and 12 from the Unity registry; versions were compared with the installed package descriptors. No package versions were changed. Input System **1.20.1**, URP **17.6.0**, Rider integration **3.0.40**, test framework **1.8.0**. The package manifests remain untracked as part of the existing migration work; this verification does not adopt or commit that migration baseline. Historical authoring provenance of these untracked files cannot be recovered from Git.
- Unity version/settings migration changes remain in the working tree. This task does not choose another Editor version or continue the engine/package migration.
- Validation is a local Editor smoke check, not a full input/physics playtest, formal EditMode/PlayMode test suite, player build, FIRST DEAL validation, or clean-clone reproducibility check. Gameplay and economy work remain ahead. Existing obsolete-API/importer warnings remain; no runtime errors were recorded by the final smoke check.

## Immediate milestone

1. T01 local technical verification is complete. Separately approve and checkpoint the existing engine/package migration before treating this working tree as a reproducible clean-clone baseline.
2. Implement **FIRST DEAL** as a small, complete playable scenario, using the separately approved scenario specification rather than assumptions made from this README.
3. Test behavior, economy invariants, player interactions, and the feel of the game before expanding scope.

Avoid upgrading the Unity Editor or installing packages until a specific migration/restoration task determines what is needed. Asset files use Git LFS according to `.gitattributes`; a checkout intended for editing must have the required LFS objects available.

## For contributors and Codex

- `AGENTS.md` — minimal agent constraints and working rules.
- `docs/DEVELOPMENT.md` — commit convention, checks, Unity guardrails, and investigation practices.
- `docs/ENGINEERING_STANDARD.md` — Unity/C# constraints, responsibility boundaries and verification gates.
- `Assets/Scripts/` — current gameplay code; `Assets/Scenes/` — Unity scenes; `ProjectSettings/` — project configuration.

Game design documents and FIRST DEAL technical planning were prepared outside the repository. They are **not** automatically part of this checkout. Provide their approved current versions to the implementation agent as needed.
