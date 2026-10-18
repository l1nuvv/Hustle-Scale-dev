using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Inventory.InventoryScriptableObjects;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class T02Verification
{
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static PlayerController player;
    private static CharacterController body;
    private static GrayboxInteractable target;
    private static Camera camera;
    private static GameObject blocker;
    private static int phase;
    private static int frame;
    private static float started;
    private static Vector3 origin;
    private static float walkDistance;
    private static bool failed;
    private static bool completed;
    private static InventoryManager inventory;
    private static ItemPickup pickup;
    private static Item pickedItem;
    private static int beforeAmount;
    private static int pickupAmount;
    private const string LogPath = "Logs/Codex-T02-verification.log";

    static T02Verification()
    {
        EditorApplication.playModeStateChanged += PlayState;
        if (SessionState.GetBool("T02.running", false))
            Application.logMessageReceived += LogError;
    }

    [MenuItem("Tools/T02/Verify Graybox")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(FirstDealGrayboxBuilder.ScenePath);
        File.WriteAllText(LogPath, "T02 Unity " + Application.unityVersion + "\n");
        SessionState.SetBool("T02.failed", false);
        SessionState.SetBool("T02.completed", false);
        SessionState.SetBool("T02.running", true);
        EditorApplication.isPlaying = true;
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("T02.running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                body = player.GetComponent<CharacterController>();
                target = UnityEngine.Object.FindAnyObjectByType<GrayboxInteractable>();
                camera = Camera.main;
                keyboard = InputSystem.AddDevice<Keyboard>("T02Keyboard");
                mouse = InputSystem.AddDevice<Mouse>("T02Mouse");
                player.SetCursorLocked(true);
                failed = false;
                completed = false;
                phase = 0;
                frame = -1;
                started = Time.time;
                Application.logMessageReceived -= LogError;
                Application.logMessageReceived += LogError;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Fail(e); }
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived -= LogError;
            SessionState.SetBool("T02.running", false);
            var passed = !SessionState.GetBool("T02.failed", false) && SessionState.GetBool("T02.completed", false);
            File.AppendAllText(LogPath, passed ? "T02 RESULT PASS\n" : "T02 RESULT FAIL or interrupted\n");
            Debug.Log(passed ? "T02 RESULT PASS" : "T02 RESULT FAIL or interrupted");
            EditorSceneManager.OpenScene(FirstDealGrayboxBuilder.ScenePath);
        }
    }
    private static void LogError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            failed = true;
            SessionState.SetBool("T02.failed", true);
            File.AppendAllText(LogPath, "RUNTIME ERROR " + message + "\n" + stack + "\n");
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }
    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static void Teleport(Vector3 position)
    {
        body.enabled = false;
        player.transform.SetPositionAndRotation(position, Quaternion.identity);
        body.enabled = true;
        player.SetCursorLocked(true);
        Physics.SyncTransforms();
    }
    private static void Next()
    {
        phase++;
        started = Time.time;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || frame == Time.frameCount) return;
        frame = Time.frameCount;
        if (Time.time - started < 0.3f) return;
        try
        {
            switch (phase)
            {
                case 0:
                    Check(body.isGrounded && Mathf.Abs(player.transform.position.y) < 0.1f, "spawn settles on floor");
                    Check(camera.transform.parent == player.transform && Mathf.Abs(camera.transform.position.y - 1.65f) < 0.15f, "camera at eye height");
                    Check(player.GetComponent<PlayerRaycast>().FocusLabel.Length > 0, "focus prompt visible in reach");
                    Keys(Key.E); Next(); break;
                case 1:
                    Check(target.InteractionCount == 1 && target.IsOn, "held E invokes once");
                    Keys(); Next(); break;
                case 2: Keys(Key.E); Next(); break;
                case 3:
                    Check(target.InteractionCount == 2 && !target.IsOn, "second E switches light off");
                    Keys();
                    blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blocker.transform.position = new Vector3(0, 1.5f, -4.8f);
                    blocker.transform.localScale = new Vector3(2, 3, 0.2f);
                    Physics.SyncTransforms(); Next(); break;
                case 4: Keys(Key.E); Next(); break;
                case 5:
                    Check(target.InteractionCount == 2, "solid wall blocks E");
                    UnityEngine.Object.Destroy(blocker);
                    Keys(); Teleport(new Vector3(0, 0.02f, -7.5f)); Next(); break;
                case 6: Keys(Key.E); Next(); break;
                case 7:
                    Check(target.InteractionCount == 2, "E cannot reach distant target");
                    Keys(); Teleport(new Vector3(2, 0.02f, -6)); Next(); break;
                case 8: origin = player.transform.position; Keys(Key.W); Next(); break;
                case 9:
                    walkDistance = Vector3.Distance(origin, player.transform.position);
                    Check(walkDistance > 0.9f && walkDistance < 2.4f, "W moves at walking speed");
                    Keys(); Teleport(new Vector3(2, 0.02f, -6)); Next(); break;
                case 10: origin = player.transform.position; Keys(Key.W, Key.D); Next(); break;
                case 11:
                    Check(Mathf.Abs(Vector3.Distance(origin, player.transform.position) - walkDistance) < 0.35f, "diagonal speed is normalized");
                    Keys(); Teleport(new Vector3(2, 0.02f, -6)); Next(); break;
                case 12: origin = player.transform.position; Keys(Key.W, Key.LeftShift); Next(); break;
                case 13:
                    Check(Vector3.Distance(origin, player.transform.position) > walkDistance * 1.25f, "Shift increases movement speed");
                    Keys(); Teleport(new Vector3(2, 0.02f, 7)); Next(); break;
                case 14: Keys(Key.W); Next(); break;
                case 15:
                    Check(player.transform.position.z < 7.6f && body.isGrounded, "north wall stops capsule");
                    Keys(); Teleport(new Vector3(-4, 0.02f, -4.5f)); Next(); break;
                case 16: Keys(Key.W); Next(); break;
                case 17:
                    if (Time.time - started < 1.05f) return;
                    Check(player.transform.position.y > 0.3f && player.transform.position.z > -1.5f, "capsule climbs 0.2m steps");
                    Keys(); Teleport(new Vector3(2, 0.02f, -6)); Next(); break;
                case 18:
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(200, 10000) });
                    Next(); break;
                case 19:
                    Check(Mathf.Abs(Mathf.DeltaAngle(0, player.transform.eulerAngles.y)) > 10, "mouse turns player yaw");
                    Check(Vector3.Dot(camera.transform.forward, Vector3.up) > 0.98f, "camera pitch clamps below vertical");
                    player.SetMovementEnabled(false);
                    player.SetCameraControlEnabled(false);
                    origin = player.transform.position;
                    Keys(Key.W, Key.E); Next(); break;
                case 20:
                    Check(Vector3.Distance(origin, player.transform.position) < 0.05f && target.InteractionCount == 2, "UI lock blocks movement and interaction");
                    Keys(); player.SetMovementEnabled(true); player.SetCameraControlEnabled(true); Next(); break;
                case 21: Keys(Key.Escape); Next(); break;
                case 22:
                    Check(!player.CanInteract && Cursor.lockState == CursorLockMode.None, "Esc releases cursor and gameplay");
                    Keys(); InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); Next(); break;
                case 23:
                    Check(player.CanInteract, "click resumes cursor capture");
                    Keys();
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
                    Next(); break;
                case 24:
                    inventory = UnityEngine.Object.FindAnyObjectByType<InventoryManager>();
                    Check(inventory != null && inventory.slots.Count > 0, "prototype inventory initializes");
                    player = inventory.playerController;
                    player.SetCursorLocked(true);
                    Keys(Key.Tab); Next(); break;
                case 25:
                    Check(inventory.isOpened && !player.CanInteract, "prototype Tab opens inventory once and blocks gameplay");
                    Keys(); Next(); break;
                case 26: Keys(Key.Tab); Next(); break;
                case 27:
                    Check(!inventory.isOpened && player.CanInteract, "prototype second Tab restores gameplay");
                    Keys();
                    camera = Camera.main;
                    pickup = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.item != null);
                    pickedItem = pickup.item;
                    pickupAmount = pickup.amount;
                    beforeAmount = inventory.slots.Where(s => s.item == pickedItem).Sum(s => s.amount);
                    if (pickup.TryGetComponent<Rigidbody>(out var pickupBody)) pickupBody.isKinematic = true;
                    // Keep the ray origin outside the old prefab's large trigger.
                    var sphere = pickup.GetComponent<SphereCollider>();
                    var scale = pickup.transform.lossyScale;
                    var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    pickup.transform.position = camera.transform.position + camera.transform.forward * (radius + 0.5f);
                    Physics.SyncTransforms(); Next(); break;
                case 28:
                    Check(Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 1f,
                            1 << pickup.gameObject.layer, QueryTriggerInteraction.Collide) && hit.collider.gameObject == pickup.gameObject,
                        "prototype trigger surface is in reach before E");
                    Keys(Key.E); Next(); break;
                case 29:
                    Check(pickup == null && inventory.slots.Where(s => s.item == pickedItem).Sum(s => s.amount) == beforeAmount + pickupAmount,
                        "prototype E picks trigger-collider item exactly once");
                    Check(!failed, "no runtime errors during suite");
                    completed = true;
                    SessionState.SetBool("T02.completed", completed);
                    EditorApplication.isPlaying = false; break;
            }
        }
        catch (Exception e) { Fail(e); }
    }
    private static void Fail(Exception e)
    {
        failed = true;
        SessionState.SetBool("T02.failed", true);
        File.AppendAllText(LogPath, "FAIL phase " + phase + ": " + e + "\n");
        Debug.LogException(e);
        EditorApplication.isPlaying = false;
    }
}
