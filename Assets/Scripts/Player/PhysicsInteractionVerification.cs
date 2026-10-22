using System;
using System.Collections;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Profiling;

namespace Player
{
    public sealed partial class CityPlayerVerification
    {
        // Shared by the existing isolated Editor harness and opt-in Development Player.
        // Assertions exercise the real adapter/PhysX; they are not NUnit test counts.
        public static IEnumerator PhysicsChecks(CityTradeView view, Keyboard keyboard,
            Action<bool, string> check, Action<string> log)
        {
            var controller = view.GetComponent<PlayerController>();
            var saveController = view.GetComponent<CitySaveController>();
            var walking = view.GetComponent<CityGreyboxWalk>();
            foreach (var crate in new[] { true, false })
            {
                saveController.NewGame();
                walking.SetPaused(false);
                controller.RestorePose(new Vector3(-65, 0.03f, 25), 0, 85);
                var session = view.Session;
                check(session.PurchaseLot(out _), "purchase for placement");
                check(crate ? session.ToggleCrate(out _) : session.ToggleUnit(0, out _), "take placement object");
                Physics.SyncTransforms();
                var before = JsonUtility.ToJson(session.CaptureCity(saveController.World));
                check(!view.DropHeld(), "Q at feet must reject overlap with the walking capsule: " + (crate ? "crate" : "lamp"));
                check(before == JsonUtility.ToJson(session.CaptureCity(saveController.World)), "rejected placement preserves model/world pose");
                controller.RestorePose(new Vector3(-65, 0.03f, 25), 45, 0);
                Physics.SyncTransforms();
                check(view.DropHeld(), "Q on clear ground still succeeds");
                Physics.SyncTransforms();
                var collider = (crate ? view.Crate : view.Lamp(0)).GetComponent<Collider>();
                var body = controller.GetComponent<CharacterController>();
                check(collider.enabled && !Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    collider, collider.transform.position, collider.transform.rotation, out _, out _), "placed object clears player");
                check(crate ? !session.IsCrateCarried : session.Units[0].Location == FlashlightLocation.World,
                    "placement changes one physical/model location");
                log("PASS Q self overlap rejection and safe rotated placement: " + (crate ? "crate" : "lamp"));
            }
            saveController.NewGame();
            walking.SetPaused(false);
            var input = controller.GetComponent<PlayerInput>();
            var oldMouseEnabled = input.MouseInputEnabled;
            var oldCaptureDelta = Time.captureDeltaTime;
            var oldTimeScale = Time.timeScale;
            var oldTargetFps = Application.targetFrameRate;
            var oldVsync = QualitySettings.vSyncCount;
            if (!Application.isEditor)
                foreach (var argument in Environment.GetCommandLineArgs())
                    if (argument.StartsWith("-physics-fps=", StringComparison.Ordinal) &&
                        int.TryParse(argument.Substring(13), out var fps) && fps >= 10 && fps <= 240)
                    { QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps; }
            var mouse = InputSystem.AddDevice<Mouse>("Physics verification mouse");
            var target = new GameObject("Physics audit parent interaction", typeof(GrayboxInteractable));
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.transform.SetParent(target.transform, false);
            shape.transform.localScale = Vector3.one * 0.35f;
            var interaction = target.GetComponent<GrayboxInteractable>();
            var feet = new Vector3(-65, 0.03f, 25);
            GameObject obstruction = null;
            try
            {
                input.MouseInputEnabled = true;
                controller.RestorePose(feet, 90, 0);
                var camera = controller.GetComponentInChildren<Camera>();
                target.transform.position = feet + Vector3.up * 1.65f + Vector3.forward * 2;
                Physics.SyncTransforms();
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(-750, 0) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(Mathf.Abs(Mathf.DeltaAngle(controller.transform.eulerAngles.y, 0)) < 0.01f,
                    "same-frame mouse input reaches target yaw");
                check(interaction.InteractionCount == 1, "same-frame turn and E use the updated camera and child collider");
                for (var i = 0; i < 4; i++) yield return null;
                check(interaction.InteractionCount == 1, "holding E does not repeat");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 2, "a second press invokes exactly once");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(750, 0) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 2, "turning away and E cannot activate the previous target");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                controller.RestorePose(feet, 0, 0);
                obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstruction.transform.position = feet + Vector3.up * 1.5f + Vector3.forward;
                obstruction.transform.localScale = new Vector3(1, 3, 0.2f);
                Physics.SyncTransforms();
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 2 && controller.GetComponent<PlayerRaycast>().FocusLabel == "",
                    "opaque obstacle blocks action and prompt");
                UnityEngine.Object.Destroy(obstruction); obstruction = null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                target.transform.position = camera.transform.position + Vector3.forward * 2.665f;
                Physics.SyncTransforms();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 3, "collider front at 2.49m is in reach");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                target.transform.position = camera.transform.position + Vector3.forward * 2.685f;
                Physics.SyncTransforms();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 3, "collider front at 2.51m is out of reach");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                target.transform.position = camera.transform.position + Vector3.forward * 2;
                Physics.SyncTransforms();
                walking.SetPaused(true);
                var pausedPosition = controller.transform.position;
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E, Key.W));
                yield return null; yield return null;
                check(interaction.InteractionCount == 3 && Vector3.Distance(pausedPosition, controller.transform.position) < 0.001f,
                    "pause UI blocks E and movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                walking.SetPaused(false);
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(interaction.InteractionCount == 4, "E works after UI closes");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                UnityEngine.Object.Destroy(target);
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(controller.GetComponent<PlayerRaycast>().FocusLabel == "", "destroyed target leaves no stale prompt/action");
                log("PASS current camera, child collider, hold/repress, wall, range, UI and destroyed target");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                input.MouseInputEnabled = false;
                using (var simulate = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate", 64))
                using (var queries = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics Queries", 64))
                using (var sync = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.SyncTransforms", 64))
                {
                    var realElapsed = 0f;
                    var maxFrame = 0f;
                    for (var i = 0; i < 32; i++)
                    {
                        yield return null;
                        realElapsed += Time.unscaledDeltaTime;
                        maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime);
                    }
                    log("REAL frame rate target=" + Application.targetFrameRate + " measured=" + (32 / realElapsed).ToString("F1") +
                        "Hz maxFrame=" + (maxFrame * 1000).ToString("F2") + "ms; Editor=" + Application.isEditor);
                    foreach (var recorder in new[] { simulate, queries, sync })
                    {
                        var samples = recorder.ToArray();
                        log("PROFILER " + recorder.Valid + " samples=" + samples.Length + " mean=" +
                            (samples.Length == 0 ? "n/a" : samples.Average(s => s.Value).ToString("F1")));
                    }
                    log("PROFILER order: Physics.Simulate ns/frame; Physics Queries count/frame; Physics.SyncTransforms ns/frame. Includes zero samples; no before/after speed claim.");
                }
                foreach (var fps in new[] { 15, 60, 144 })
                {
                    Time.captureDeltaTime = 1f / fps;
                    foreach (var keys in new[] { new[] { Key.W }, new[] { Key.W, Key.D }, new[] { Key.W, Key.LeftShift } })
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        controller.RestorePose(feet, 0, 0);
                        yield return null; yield return null;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                        yield return null; yield return null;
                        var start = controller.transform.position;
                        var elapsed = 0f;
                        for (var i = 0; i < 12; i++) { yield return null; elapsed += Time.deltaTime; }
                        var distance = Vector3.ProjectOnPlane(controller.transform.position - start, Vector3.up).magnitude;
                        var speed = keys.Length > 1 && keys[1] == Key.LeftShift ? 6 : 4;
                        check(Mathf.Abs(distance - elapsed * speed) < 0.08f,
                            "movement speed/diagonal at simulated " + fps + "Hz: " + distance + "m / " + elapsed + "s");
                        log("MOVE simulated " + fps + "Hz " + string.Join(",", keys) + ": " + distance.ToString("F4") + "m / " + elapsed.ToString("F4") + "s");
                    }
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                Time.timeScale = 0;
                yield return null; yield return null;
                var frozen = controller.transform.position;
                yield return null; yield return null;
                check(Vector3.Distance(frozen, controller.transform.position) < 0.001f, "timeScale zero freezes movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Time.timeScale = oldTimeScale;
                Time.captureDeltaTime = oldCaptureDelta;
                CollisionChecks(controller, check, log);
            }
            finally
            {
                Time.timeScale = oldTimeScale;
                Time.captureDeltaTime = oldCaptureDelta;
                Application.targetFrameRate = oldTargetFps;
                QualitySettings.vSyncCount = oldVsync;
                input.MouseInputEnabled = oldMouseEnabled;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (target != null) UnityEngine.Object.Destroy(target);
                if (obstruction != null) UnityEngine.Object.Destroy(obstruction);
                walking.SetPaused(true);
            }
        }

        private static void CollisionChecks(PlayerController controller, Action<bool, string> check, Action<string> log)
        {
            var root = new GameObject("Temporary physics audit geometry");
            var body = controller.GetComponent<CharacterController>();
            var origin = new Vector3(440, 0, 440);
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(root.transform);
                floor.transform.position = origin + Vector3.down * 0.25f;
                floor.transform.localScale = new Vector3(20, 0.5f, 20);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = origin + new Vector3(0, 1, 2);
                wall.transform.localScale = new Vector3(8, 2, 0.2f);
                Physics.SyncTransforms();
                controller.RestorePose(origin + Vector3.up * 0.03f, 0, 0);
                for (var i = 0; i < 30; i++) body.Move(Vector3.forward * 0.1f + Vector3.down * 0.03f);
                check(controller.transform.position.z > 441.3f && controller.transform.position.z < 441.7f,
                    "wall stops real CharacterController");
                controller.RestorePose(origin + Vector3.up * 0.03f, 0, 0);
                for (var i = 0; i < 40; i++) body.Move(new Vector3(1, 0, 1).normalized * 0.1f + Vector3.down * 0.03f);
                check(controller.transform.position.x > 442 && controller.transform.position.z < 441.7f,
                    "diagonal movement slides along wall");
                var corner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                corner.transform.SetParent(root.transform);
                corner.transform.position = origin + new Vector3(2, 1, 0);
                corner.transform.localScale = new Vector3(0.2f, 2, 8);
                Physics.SyncTransforms();
                controller.RestorePose(origin + Vector3.up * 0.03f, 0, 0);
                for (var i = 0; i < 40; i++) body.Move(new Vector3(1, 0, 1).normalized * 0.1f + Vector3.down * 0.03f);
                check(controller.transform.position.x > 441.3f && controller.transform.position.x < 441.7f &&
                    controller.transform.position.z > 441.3f && controller.transform.position.z < 441.7f, "corner stops both axes");
                for (var i = 0; i < 20; i++) body.Move(Vector3.back * 0.1f + Vector3.down * 0.03f);
                check(controller.transform.position.z < 440, "player can back out of the corner");
                corner.SetActive(false);
                wall.SetActive(false);
                var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
                left.transform.SetParent(root.transform); right.transform.SetParent(root.transform);
                left.transform.localScale = right.transform.localScale = new Vector3(0.1f, 2, 4);
                foreach (var width in new[] { 0.8f, 0.4f })
                {
                    left.transform.position = origin + new Vector3(-(width + 0.1f) / 2, 1, 0);
                    right.transform.position = origin + new Vector3((width + 0.1f) / 2, 1, 0);
                    Physics.SyncTransforms();
                    controller.RestorePose(origin + new Vector3(0, 0.03f, -3), 0, 0);
                    for (var i = 0; i < 65; i++) body.Move(Vector3.forward * 0.1f + Vector3.down * 0.03f);
                    check(width > 0.6f ? controller.transform.position.z > 442.8f : controller.transform.position.z < 438,
                        "capsule respects corridor width " + width);
                }
                log("PASS real capsule wall, slide, corner/reverse and 0.8m/0.4m corridors in temporary geometry");
                foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                {
                    controller.RestorePose(direction * 490 + Vector3.up * 0.05f, Quaternion.LookRotation(direction).eulerAngles.y, 0);
                    for (var i = 0; i < 240; i++) body.Move(direction * 0.1f + Vector3.down * 0.03f);
                    check(Vector3.Dot(controller.transform.position, direction) > 495 &&
                        Mathf.Abs(controller.transform.position.x) < 496 && Mathf.Abs(controller.transform.position.z) < 496 &&
                        controller.transform.position.y > -0.1f, "actual city boundary and floor " + direction + ": " + controller.transform.position);
                }
                log("PASS actual city capsule boundaries on all four outer road axes");
            }
            finally
            {
                controller.RestorePose(new Vector3(-65, 0.03f, 25), 180, 0);
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
