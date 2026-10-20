using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Frame-driven integration check. Sends real Input System states through the existing
// player map and raycast; only camera poses are arranged to make captures repeatable.
[InitializeOnLoad]
public static class ArtDirectionTestVerification
{
    private const string Output = "docs/ArtDirection/UnityTest";
    private static int phase, checks;
    private static double next, deadline;
    private static PlayerController player;
    private static ArtDirectionTestView view;
    private static Vector3 before;
    private static bool oldBackground;
    private static bool oldMouseInput;
    private static InputSettings.BackgroundBehavior oldInputBackground;
    private static string[] identities;
    private static Action afterCapture;
    private static int originalSizeIndex;
    private static bool sizeChanged;
    private static readonly List<int> temporarySizes = new List<int>();
    private static Animator workerAnimator;
    private static Transform workerSpine;
    private static Quaternion workerPose;
    private static float workerTime;

    static ArtDirectionTestVerification() => EditorApplication.playModeStateChanged += state =>
    {
        if (state == PlayModeStateChange.ExitingPlayMode) End();
    };

    [MenuItem("Tools/Art Direction/Verify Current Play Session")]
    public static void Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ArtDirectionTestBuilder.ScenePath)
            throw new InvalidOperationException("Open the art sample and enter Play Mode first.");
        End();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/verification.txt", "Unity " + Application.unityVersion + " · Input System frame-driven integration\n");
        player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        oldMouseInput = player.GetComponent<Player.PlayerInput>().MouseInputEnabled;
        player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
        view = UnityEngine.Object.FindAnyObjectByType<ArtDirectionTestView>();
        identities = view.Session.Units.Select(u => u.InstanceId).ToArray();
        phase = checks = 0; next = Time.timeAsDouble + 0.5; deadline = EditorApplication.timeSinceStartup + 90;
        var game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        originalSizeIndex = (int)game.GetType().GetProperty("selectedSizeIndex").GetValue(game, null);
        Resolution(1600, 900);
        oldBackground = Application.runInBackground; Application.runInBackground = true;
        oldInputBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.timeAsDouble < next) return;
        if (afterCapture != null)
        {
            var action = afterCapture; afterCapture = null; action();
            next = Time.timeAsDouble + 0.22; return;
        }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Frame timeout at phase " + phase);
            var s = view.Session;
            var wait = 0.22;
            switch (phase++)
            {
                case 0:
                    player.SetCursorLocked(true); Pose(new Vector3(0.1f, 0.03f, -0.7f), new Vector3(0.1f, 1.05f, 2.1f));
                    Check(s.Units.All(u => !u.IsTested && u.Location == FlashlightLocation.Crate), "three unknown owned lamps at start");
                    var worker = GameObject.Find("WorkshopWorker");
                    var skins = worker.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Check(skins.Length == 8 && worker.GetComponentsInChildren<MeshRenderer>().Length == 0, "NPC uses imported Blender skinned meshes, no primitive body parts");
                    Check(skins.All(r => r.sharedMesh.boneWeights.Length == r.sharedMesh.vertexCount && r.bones.Length > 0), "all character meshes have real skin weights and bones");
                    workerAnimator = worker.GetComponentInChildren<Animator>();
                    Check(workerAnimator.avatar != null && workerAnimator.avatar.isValid && workerAnimator.runtimeAnimatorController != null, "character has valid Generic avatar and idle controller");
                    Check(skins.SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit") &&
                        skins.First(r => r.name == "Stylized head").sharedMaterials.Any(m => m.name == "Worker_Pupil") &&
                        skins.First(r => r.name == "Stylized head").sharedMaterials.Single(m => m.name == "Worker_EyeWhite").mainTexture == null &&
                        skins.First(r => r.name == "Stylized head").sharedMaterials.Single(m => m.name == "Worker_EyeWhite").GetColor("_BaseColor").r > .8f,
                        "character uses URP materials and untextured white geometric eyes");
                    workerSpine = worker.GetComponentsInChildren<Transform>().First(t => t.name == "spine_03");
                    workerPose = workerSpine.localRotation;
                    workerTime = workerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    break;
                case 1: player.SetCursorLocked(true); Capture("01-workshop"); before = player.transform.position; SendKeys(Key.W); wait = 0.35; break;
                case 2: SendKeys(); Check(player.transform.position.z > before.z + 0.25f, "WASD moves via existing Input System controller"); break;
                case 3: SendKeys(Key.W); wait = 0.9; break;
                case 4:
                    SendKeys(); Check(player.transform.position.z < 1.4f && player.transform.position.z > 0.6f, "table collision blocks forward walk, approach reachable");
                    Check(player.transform.position.y > -0.1f, "floor collision supports player");
                    Check(Quaternion.Angle(workerPose, workerSpine.localRotation) > .02f, "idle visibly deforms the actual skeleton over real frames");
                    AimLamp(0); break;
                case 5: SendKeys(Key.E); break;
                case 6: SendKeys(); Check(s.InHands == s.Units[0], "E raycast picks the intended lamp"); break;
                case 7: SendKeys(Key.F); break;
                case 8:
                    SendKeys(); Check(s.InHands.IsPowered && !s.InHands.IsTested, "F lights lamp without revealing professional diagnosis");
                    Check(GameObject.Find("Worklight 01").transform.parent == Camera.main.transform, "held visual follows existing camera grip pose");
                    AimPad(); break;
                case 9: SendKeys(Key.Q); break;
                case 10: SendKeys(); Check(s.OnBench == s.Units[0] && s.InHands == null, "Q places lamp on reserved work surface"); AimTester(); break;
                case 11: SendKeys(Key.E); break;
                case 12:
                    SendKeys(); Check(s.OnBench.IsTested && s.OnBench.LastTestPassed, "E tester reports working lamp");
                    Check(Emission(0) && GameObject.Find("Worklight 01").GetComponentInChildren<Light>().enabled, "working test enables visible lens emission and real light");
                    Pose(new Vector3(0.25f, 0.03f, 0.85f), new Vector3(0.25f, 1.24f, 1.96f)); break;
                case 13: Capture("04-tested-working", () => Pose(new Vector3(0.65f, 0.03f, 1.04f), new Vector3(0.25f, 1.25f, 1.96f))); break;
                case 14: Capture("02-worklight-close", () => Pose(new Vector3(1.6f, 0.03f, 0.75f), new Vector3(1.95f, 1.3f, 2.65f))); break;
                case 15:
                    Capture("03-worker", () =>
                    {
                        Pose(new Vector3(1.95f, .03f, 2.0f), new Vector3(1.95f, 1.65f, 2.65f));
                        Capture("08-worker-face", AimBenchLamp);
                    });
                    break;
                case 16: SendKeys(Key.E); break;
                case 17: SendKeys(); Check(s.InHands == s.Units[0], "E retrieves tested lamp"); AimCrate(); break;
                case 18: SendKeys(Key.E); break;
                case 19: SendKeys(); Check(s.Units[0].Location == FlashlightLocation.Crate, "E returns same identity to crate"); AimLamp(1); break;
                case 20: SendKeys(Key.E); break;
                case 21: SendKeys(); Check(s.InHands == s.Units[1] && !s.InHands.IsTested, "second shell remains unknown on pickup"); AimPad(); break;
                case 22: SendKeys(Key.Q); break;
                case 23: SendKeys(); Check(s.OnBench == s.Units[1], "second lamp placed without duplication"); AimTester(); break;
                case 24: SendKeys(Key.E); break;
                case 25:
                    SendKeys(); Check(s.OnBench.IsTested && !s.OnBench.LastTestPassed && !s.OnBench.IsPowered, "existing diagnostic detects battery fault");
                    Check(!Emission(1) && !GameObject.Find("Worklight 02").GetComponentInChildren<Light>().enabled, "fault result leaves lens and beam off");
                    Pose(new Vector3(0.25f, 0.03f, 0.85f), new Vector3(0.25f, 1.24f, 1.96f)); break;
                case 26: Capture("05-battery-fault", () => SendKeys(Key.Escape)); break;
                case 27: SendKeys(); Check(!player.CanInteract && Cursor.lockState == CursorLockMode.None, "Esc releases gameplay focus and cursor"); break;
                case 28: SendKeys(Key.F); break;
                case 29: SendKeys(); Check(!s.OnBench.IsPowered, "unfocused F cannot mutate lamp"); InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left)); break;
                case 30: InputSystem.QueueStateEvent(Mouse.current, new MouseState()); Check(player.CanInteract, "mouse click restores gameplay focus"); break;
                case 31:
                    Pose(new Vector3(0, 0.03f, -2.3f), new Vector3(1.02f, 1.05f, 1.92f));
                    view.Act(ArtTestInteraction.ActionKind.Lamp, 2);
                    Check(s.Units[2].Location == FlashlightLocation.Crate && s.InHands == null, "remote interaction leaves item state intact");
                    Check(identities.SequenceEqual(s.Units.Select(u => u.InstanceId)) && s.Balance == 120, "all moves preserve identities and fixture money");
                    Pose(new Vector3(2.55f, 0.03f, 1.35f), new Vector3(2.55f, 1.68f, 7));
                    break;
                case 32: SendKeys(Key.W); wait = 0.9; break;
                case 33:
                    SendKeys(); Check(player.transform.position.z > 3.6f, "doorway remains walkable past the NPC into courtyard");
                    Check(player.transform.position.y > -0.1f, "courtyard paving supports player");
                    Pose(new Vector3(0.1f, 0.03f, -0.7f), new Vector3(0.1f, 1.05f, 2.1f)); break;
                case 34: Check(Screen.width == 1600 && Screen.height == 900, "Game View renders at 16:9 1600x900"); Layout(); Resolution(1600, 1000); break;
                case 35: Check(Screen.width == 1600 && Screen.height == 1000, "Game View renders at 16:10 1600x1000"); Layout(); Capture("06-hud-16x10"); break;
                case 36: Resolution(2100, 900); break;
                case 37: Check(Screen.width == 2100 && Screen.height == 900, "Game View renders at 21:9 2100x900"); Layout(); Capture("07-hud-21x9"); break;
                case 38:
                    Check(workerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > workerTime + .5f, "worker idle continues looping during gameplay");
                    File.AppendAllText(Output + "/verification.txt", "PASS: " + checks + " checks. Screenshots captured from Unity Game View with uGUI.\n");
                    Debug.Log("ART TEST PASS: " + checks + " checks"); End(); return;
            }
            next = Time.timeAsDouble + wait;
        }
        catch (Exception error)
        {
            File.AppendAllText(Output + "/verification.txt", "FAIL phase " + (phase - 1) + ": " + error.Message + "\n");
            File.AppendAllText(Output + "/verification.txt", "Pose " + player.transform.position + " / interactive " + player.CanInteract + " / move " + player.GetComponent<Player.PlayerInput>().MoveInput + "\n");
            Debug.LogException(error); End();
        }
    }

    private static void Pose(Vector3 position, Vector3 target)
    {
        var rotation = Quaternion.LookRotation(target - (position + Vector3.up * 1.65f)).eulerAngles;
        player.RestorePose(position, rotation.y, Mathf.DeltaAngle(0, rotation.x));
    }

    private static void AimLamp(int index) => Pose(new Vector3(-1.75f + index * 0.4f, 0.03f, 0.85f), new Vector3(-1.75f + index * 0.4f, 1.39f, 2.05f));
    private static void AimPad() => Pose(new Vector3(0.25f, 0.03f, 0.8f), new Vector3(0.25f, 0.975f, 1.73f));
    private static void AimTester() => Pose(new Vector3(0.85f, 0.03f, 0.9f), new Vector3(1.02f, 1.04f, 1.92f));
    private static void AimBenchLamp() => Pose(new Vector3(0.25f, 0.03f, 0.85f), new Vector3(0.25f, 1.25f, 1.96f));
    private static void AimCrate() => Pose(new Vector3(-1.35f, 0.03f, 0.8f), new Vector3(-1.35f, 1.13f, 1.79f));
    private static void SendKeys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    private static void Capture(string name, Action nextAction = null)
    {
        ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
        afterCapture = nextAction;
    }

    private static void Layout()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in GameObject.Find("Art sample HUD").GetComponentsInChildren<UnityEngine.UI.Text>())
        {
            Check(text.preferredHeight <= text.rectTransform.rect.height + 1, text.transform.parent.name + " text fits vertically");
            Check(text.preferredWidth <= text.rectTransform.rect.width + 1, text.transform.parent.name + " text fits horizontally");
            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
            Check(corners.All(c => c.x >= 0 && c.x <= Screen.width && c.y >= 0 && c.y <= Screen.height), text.transform.parent.name + " inside screen");
        }
    }

    private static bool Emission(int index)
    {
        var renderer = GameObject.Find("Worklight " + (index + 1).ToString("00")).transform.Find("Inset diffuser").GetComponent<Renderer>();
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
        return renderer.sharedMaterial.IsKeywordEnabled("_EMISSION") && block.GetColor("_EmissionColor").maxColorComponent > 1;
    }

    // Local Editor-only Game View sizing, reflected against the installed Editor.
    private static void Resolution(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var instance = type.BaseType.GetProperty("instance").GetValue(null, null);
        var group = type.GetProperty("currentGroup").GetValue(instance, null);
        var sizeType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), Enum.Parse(sizeType, "FixedResolution"), width, height, "Art test " + width + "x" + height);
        var index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        temporarySizes.Add(index);
        var game = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        game.GetType().GetProperty("selectedSizeIndex").SetValue(game, index, null); game.Repaint(); sizeChanged = true;
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++; File.AppendAllText(Output + "/verification.txt", "PASS " + description + "\n");
    }

    private static void End()
    {
        EditorApplication.update -= Tick;
        afterCapture = null;
        if (Keyboard.current != null) SendKeys();
        if (player != null)
        {
            Application.runInBackground = oldBackground;
            player.GetComponent<Player.PlayerInput>().MouseInputEnabled = oldMouseInput;
            InputSystem.settings.backgroundBehavior = oldInputBackground;
        }
        player = null;
        if (sizeChanged)
        {
            var game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            game.GetType().GetProperty("selectedSizeIndex").SetValue(game, originalSizeIndex, null); sizeChanged = false;
            var sizes = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var instance = sizes.BaseType.GetProperty("instance").GetValue(null, null);
            var group = sizes.GetProperty("currentGroup").GetValue(instance, null);
            for (var i = temporarySizes.Count - 1; i >= 0; i--)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { temporarySizes[i] });
            temporarySizes.Clear();
        }
    }
}
