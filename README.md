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
2. T02 provides `Assets/Scenes/FirstDealGraybox.unity`: a 20 x 16 metre enclosed blockout, a 1.4 metre passage, work area/counter placeholders, collision crate, 0.2 metre steps and an interactive test light. The FPS capsule is 1.8 metres tall / 0.6 metres wide, eye height 1.65 metres, walk speed 4 m/s, sprint 6 m/s, interaction reach 2.5 metres. Controls: WASD, mouse, Left Shift, E; Escape releases the cursor and a left click resumes capture. This scene is opened directly in the Editor; existing build scene settings are preserved.
3. Test behavior, economy invariants, player interactions, and the feel of the game before expanding scope.

**T02 verification (2026-10-18, Unity 6000.6.0f1):** compilation and the Editor-driven Play Mode suite passed 23 checks, including gravity, eye height, prompt, single E while held, repeat E, wall occlusion, reach, walking/diagonal/sprint, capsule collision and steps, mouse yaw/pitch, control lock and cursor release/resume. A regression check in `SampleScene` also confirmed Tab open/close and exactly one item quantity added by E against the original trigger-collider pickup. Repeat with **Tools > T02 > Verify Graybox**; local output: `Logs/Codex-T02-verification.log`. These are integration checks using virtual Input System devices, not a formal NUnit suite, player build or human playtest. Its test light only proves interaction; the separate T03 workshop implements item diagnosis and repair below.

**T03 workshop (2026-10-18):** open `Assets/Scenes/FirstDealWorkshop.unity` and enter Play Mode. The workshop beyond the passage contains a portable crate, three lamp selectors, a one-lamp workbench, TEST, BUY BATTERY and REPLACE BATTERY controls. Use E while looking at a control; E on the bench places/retrieves the held lamp. A lamp selector takes/returns that same instance. Carrying the crate moves all stored lamps; the dock control returns it to its original dock. Two lamps pass; one blinks and switches off. Transport preserves the result. A $50 spare battery remains in stock until repair consumes it and adds 240 seconds; repair invalidates the previous result and requires a new test.

This is explicitly a **post-purchase T03 fixture**: three owned lamps and $120. Its repair-time total is an integration value, not the running shift clock. Seller purchase, orders, delivery, deadlines and shared economy/clock integration remain T04. Rules were read from FIRST DEAL v1.0 in the supplied v0.3.1 reference archive; the scene does not implement the entire scenario.

**T03 verification:** scripts compiled in Unity 6000.6.0f1; **Tools > T03 > Verify Workshop** passed 51 model and Play Mode integration checks. These cover identity conservation, hand/bench/crate transitions, hidden diagnosis, consumable and money boundaries, repeated repair rejection, invalidated tests, E dispatch, steady light, fault blinking, retest and HUD. Local log: `Logs/Codex-T03-verification.log`. The runner uses virtual keyboard input and temporarily isolated background input settings, then restores them. This is an Editor-driven verification utility, not a formal NUnit suite, player build or independent human playtest.

Avoid upgrading the Unity Editor or installing packages until a specific migration/restoration task determines what is needed. Asset files use Git LFS according to `.gitattributes`; a checkout intended for editing must have the required LFS objects available.

## For contributors and Codex

- `AGENTS.md` — minimal agent constraints and working rules.
- `docs/DEVELOPMENT.md` — commit convention, checks, Unity guardrails, and investigation practices.
- `docs/ENGINEERING_STANDARD.md` — Unity/C# constraints, responsibility boundaries and verification gates.
- `Assets/Scripts/` — current gameplay code; `Assets/Scenes/` — Unity scenes; `ProjectSettings/` — project configuration.

Game design documents and FIRST DEAL technical planning were prepared outside the repository. They are **not** automatically part of this checkout. Provide their approved current versions to the implementation agent as needed.
