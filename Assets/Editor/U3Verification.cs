using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Runs the actual scene and UI input module; does not replace independent player observation.
[InitializeOnLoad]
public static class U3Verification
{
    private const string RunningKey = "U3.running";
    private const string LogPath = "Logs/Codex-U3-verification.log";
    private static readonly Vector2Int[] Sizes = {
        new Vector2Int(1024, 768), new Vector2Int(1280, 720),
        new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080)
    };
    private static FirstDealView deal;
    private static WorkshopView workshop;
    private static PlayerController player;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static IEnumerator steps;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode inputBehavior;
    private static bool runInBackground, failed, completed;
    private static int checks, frame, originalSize, customSize;
    private static double started, nextStep;
    private static object sizeGroup;
    private static EditorWindow gameView;
    private static readonly BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    static U3Verification() => EditorApplication.playModeStateChanged += PlayState;

    [MenuItem("Tools/U3/Verify Menus Focus and Window Sizes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "U3 Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks = 0; failed = completed = false; frame = -1;
            started = EditorApplication.timeSinceStartup; nextStep = started + 0.5;
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            background = InputSystem.settings.backgroundBehavior;
            inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.logMessageReceived += Error;
            steps = Scenario(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Error;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = inputBehavior;
            Application.runInBackground = runInBackground;
            RestoreSize();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(RunningKey, false);
            var result = completed && !failed ? "PASS" : "FAIL";
            File.AppendAllText(LogPath, "U3 RESULT " + result + ": " + checks + " assertions\n");
            Debug.Log("U3 RESULT " + result + ": " + checks + " assertions");
        }
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - started > 150)
        {
            failed = true; File.AppendAllText(LogPath, "FAIL timeout\n"); EditorApplication.isPlaying = false; return;
        }
        if (!EditorApplication.isPlaying || frame == Time.frameCount || EditorApplication.timeSinceStartup < nextStep) return;
        frame = Time.frameCount; nextStep = EditorApplication.timeSinceStartup + 0.12;
        try { if (!steps.MoveNext()) { completed = true; EditorApplication.isPlaying = false; } }
        catch (Exception e)
        {
            failed = true; File.AppendAllText(LogPath, "FAIL " + e + "\n"); EditorApplication.isPlaying = false;
        }
    }

    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true; File.AppendAllText(LogPath, "ERROR " + message + "\n" + stack + "\n");
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }

    private static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
    private static void Key(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
    private static void Release() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    private static GameObject Selected => EventSystem.current.currentSelectedGameObject;

    private static IEnumerator Scenario()
    {
        deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        Check(workshop.Session != null, "scene initializes session");
        keyboard = InputSystem.AddDevice<Keyboard>("U3Keyboard");
        mouse = InputSystem.AddDevice<Mouse>("U3Mouse");
        player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
        deal.SendMessage("OnApplicationFocus", true);
        InitSizes();
        var s = workshop.Session;
        foreach (var size in Sizes)
        {
            SetSize(size); yield return null;
            Check(Screen.width == size.x && Screen.height == size.y, "actual Game View " + size);
            deal.OpenMenu(false, true); Layout("pause");
            deal.OpenMenu(false); Layout("orders"); Navigation();
            if (size == Sizes[0]) ScreenCapture.CaptureScreenshot("Logs/U3-orders-1024x768.png");
            yield return null;
            deal.OpenMenu(true); Layout("seller"); Navigation();
            if (size == Sizes[0]) ScreenCapture.CaptureScreenshot("Logs/U3-seller-1024x768.png");
            yield return null;
            deal.OpenSummary(); Layout("summary"); Navigation();
            deal.FinishShift(); Layout("abandon confirmation"); Navigation();
            deal.CloseMenu();
            Check(!deal.MenuOpen && player.CanInteract && !s.IsPaused, "resume restores world controls " + size);
        }

        SetSize(Sizes[1]); yield return null;
        deal.OpenMenu(false); s.StartShift();
        var elapsed = s.ElapsedSeconds;
        var position = player.transform.position;
        Key(UnityEngine.InputSystem.Key.W); yield return null;
        Check(player.transform.position == position && s.ElapsedSeconds == elapsed, "menu freezes movement and clock across frames");
        Release(); yield return null;
        // W also belongs to the UI module's default navigation bindings.
        EventSystem.current.SetSelectedGameObject(Button("Resume / leave without buying").gameObject);
        // Click the empty top-left menu background, then switch back to keyboard.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(3, Screen.height - 3) }); yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(3, Screen.height - 3), buttons = 1 }); yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(3, Screen.height - 3) }); yield return null;
        Check(Selected != null, "background click preserves keyboard selection");
        Key(UnityEngine.InputSystem.Key.LeftArrow); yield return null;
        Check(Selected == Button("Shift summary").gameObject, "left arrow reaches summary; actual=" + (Selected == null ? "null" : Selected.name));
        Release(); yield return null;
        Key(UnityEngine.InputSystem.Key.Enter); yield return null;
        Check(GameObject.Find("Finish shift") != null && s.IsPaused, "Enter opens summary without advancing clock");
        Release(); yield return null;
        Key(UnityEngine.InputSystem.Key.Escape); yield return null;
        Check(!deal.MenuOpen && player.CanInteract, "Escape closes summary and restores world");
        Release(); yield return null;
        Key(UnityEngine.InputSystem.Key.Escape); yield return null;
        Check(deal.MenuOpen && s.IsPaused && !player.CanInteract, "Escape opens pause and blocks world");
        Release(); yield return null;
        deal.CloseMenu(); deal.SendMessage("OnApplicationFocus", false); yield return null;
        elapsed = s.ElapsedSeconds;
        Check(deal.MenuOpen && s.IsPaused && !player.CanInteract && Cursor.visible, "focus loss opens pause and frees cursor");
        yield return null;
        Check(s.ElapsedSeconds == elapsed, "focus loss freezes clock across frames");
        deal.SendMessage("OnApplicationFocus", true); yield return null;
        Check(deal.MenuOpen && s.IsPaused, "focus regain requires explicit resume");

        deal.OpenMenu(true);
        EventSystem.current.SetSelectedGameObject(Button("Confirm lot - $180").gameObject);
        Key(UnityEngine.InputSystem.Key.Enter); yield return null;
        Check(s.LotPurchased && s.Balance == 120 && !deal.MenuOpen, "keyboard purchases once and resumes");
        Release(); yield return null;
        deal.OpenMenu(false); EventSystem.current.SetSelectedGameObject(Button("Accept A").gameObject);
        Key(UnityEngine.InputSystem.Key.Enter); yield return null;
        Check(s.Order(DealOrderId.A).State == DealOrderState.Accepted && Selected == Button("Decline / cancel A").gameObject,
            "accept selects enabled cancellation action");
        Release(); yield return null;
        Key(UnityEngine.InputSystem.Key.Enter); yield return null;
        Check(s.Order(DealOrderId.A).State == DealOrderState.Cancelled && Selected == Button("Resume / leave without buying").gameObject,
            "cancel skips disabled actions and restores footer focus");
        Release(); yield return null;
        foreach (var size in Sizes)
        {
            SetSize(size); yield return null;
            deal.OpenMenu(false); Layout("cancelled and locked orders"); Navigation();
            deal.OpenMenu(true); Layout("purchased seller"); Navigation();
            deal.OpenSummary(); deal.FinishShift(); Layout("three lamps and abandonment confirmation"); Navigation();
            if (size == Sizes[0]) ScreenCapture.CaptureScreenshot("Logs/U3-confirm-1024x768.png");
            yield return null;
        }
        deal.FinishShift();
        Check(s.ShiftEnded && s.IsPaused && !player.CanInteract, "explicit abandonment ends shift paused");
        foreach (var size in Sizes)
        {
            SetSize(size); yield return null;
            Layout("finished shift"); Navigation();
            Check(Selected == Button("Restart shift").gameObject, "finished shift selects restart " + size);
        }
        Key(UnityEngine.InputSystem.Key.Escape); yield return null;
        Check(deal.MenuOpen && s.ShiftEnded && s.IsPaused && !player.CanInteract, "Escape cannot resume a finished shift");
        Release(); yield return null;
        SetSize(Sizes[0]); yield return null;
        ScreenCapture.CaptureScreenshot("Logs/U3-finished-1024x768.png"); yield return null;
        Key(UnityEngine.InputSystem.Key.Enter); yield return null;
        Release(); yield return null;
        workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        Check(workshop.Session != s && workshop.Session.Balance == 300 && !workshop.Session.ShiftEnded && deal.MenuOpen,
            "keyboard restart creates fresh paused session");
    }

    private static void Layout(string context)
    {
        Canvas.ForceUpdateCanvases();
        var texts = GameObject.Find("Trade menu").GetComponentsInChildren<Text>();
        foreach (var text in texts)
        {
            Check(text.preferredHeight <= text.rectTransform.rect.height + 1,
                Screen.width + "x" + Screen.height + " " + context + " text fits: " + text.transform.parent.name + "/" + text.name);
            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
            Check(corners[0].x >= -1 && corners[0].y >= -1 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                context + " text stays on screen: " + text.name);
        }
        for (var i = 0; i < texts.Length; i++)
            for (var j = i + 1; j < texts.Length; j++)
            {
                var a = new Vector3[4]; var b = new Vector3[4];
                texts[i].rectTransform.GetWorldCorners(a); texts[j].rectTransform.GetWorldCorners(b);
                Check(!new Rect(a[0], a[2] - a[0]).Overlaps(new Rect(b[0], b[2] - b[0])),
                    context + " text areas do not overlap: " + texts[i].transform.parent.name + "/" + texts[i].name + " and " + texts[j].transform.parent.name + "/" + texts[j].name);
            }
    }

    private static void Navigation()
    {
        var buttons = GameObject.Find("Trade menu").GetComponentsInChildren<Button>().Where(b => b.IsInteractable()).ToArray();
        Check(Selected != null && buttons.Any(b => b.gameObject == Selected), "selection is an enabled visible button");
        var seen = new HashSet<Selectable>(); var pending = new Queue<Selectable>();
        pending.Enqueue(Selected.GetComponent<Selectable>());
        while (pending.Count > 0)
        {
            var current = pending.Dequeue(); if (!seen.Add(current)) continue;
            foreach (var next in new[] { current.FindSelectableOnLeft(), current.FindSelectableOnRight(), current.FindSelectableOnUp(), current.FindSelectableOnDown() })
                if (next != null)
                {
                    Check(next.IsActive() && next.IsInteractable(), "navigation skips disabled/hidden buttons");
                    if (!seen.Contains(next)) pending.Enqueue(next);
                }
        }
        Check(buttons.All(b => seen.Contains(b)), "all enabled actions reachable by arrows");
    }

    // Editor-only Game View API: temporary fixed resolutions are removed on exit.
    private static void InitSizes()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        sizeGroup = sizesType.GetMethod("get_currentGroup").Invoke(sizes, null);
        gameView = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        originalSize = (int)gameView.GetType().GetProperty("selectedSizeIndex", Members).GetValue(gameView);
        customSize = (int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup, null);
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
            Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), 1024, 768, "U3 verification temporary");
        sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { size });
    }

    private static void SetSize(Vector2Int size)
    {
        var value = sizeGroup.GetType().GetMethod("GetGameViewSize").Invoke(sizeGroup, new object[] { customSize });
        value.GetType().GetProperty("width", Members).SetValue(value, size.x);
        value.GetType().GetProperty("height", Members).SetValue(value, size.y);
        gameView.GetType().GetMethod("SizeSelectionCallback", Members).Invoke(gameView, new object[] { customSize, null });
        gameView.Repaint();
    }

    private static void RestoreSize()
    {
        if (sizeGroup == null || gameView == null) return;
        gameView.GetType().GetMethod("SizeSelectionCallback", Members).Invoke(gameView, new object[] { originalSize, null });
        sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup, new object[] { customSize });
        sizeGroup = null; gameView = null;
    }
}
