using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Inventory;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static partial class M7SharpeningVerification
{
    private static bool running;
    private const string InputLog = "Logs/Codex-M7-input.log";
    [MenuItem("Tools/M7/Enter Isolated City Play Mode")]
    public static void EnterPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Preserve current scene and stop Play Mode first.");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        Directory.CreateDirectory("Temp/M7");
        SessionState.SetString("HustleScale.S2SaveOverride", Path.GetFullPath("Temp/M7/input-save.json"));
        EditorApplication.isPlaying = true;
    }
    [MenuItem("Tools/M7/Verify City Input In Play Mode")]
    public static void VerifyInput()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("M7"))
            throw new InvalidOperationException("Use M7 isolated Play Mode first.");
        if (running) return; running = true;
        Directory.CreateDirectory("Logs"); File.WriteAllText(InputLog, "M7 city actual Input System; Unity " + Application.unityVersion + "\n");
        var count = 0;
        Action<bool, string> verify = (ok, label) => { File.AppendAllText(InputLog, (ok ? "PASS " : "FAIL ") + label + "\n"); if (!ok) throw new InvalidOperationException(label); count++; };
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var trade = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>(); var player = save.GetComponent<PlayerController>();
        var eye = save.GetComponentInChildren<Camera>();
        var funded = Funded(); var world = World(); world.PlayerPosition = new Vector3(-65, .08f, 25);
        typeof(CitySaveController).GetMethod("Bind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new object[] { funded, world });
        walk.SetPaused(false);
        var t = trade.SharpeningStation; var fov = eye.fieldOfView;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent();
        var background = Application.runInBackground; Application.runInBackground = true;
        var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode; var oldFocus = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var steps = new List<Action>();
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        Action<bool, Vector2> pointer = (down, delta) => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0), delta = delta });
        Action<CityTradeKind> face = kind =>
        {
            var point = trade.Points.First(p => p.Kind == kind); var center = point.GetComponent<Collider>().bounds.center;
            player.RestorePose(new Vector3(center.x, .08f, center.z - 1.65f), 0, Mathf.Atan2(1.73f - center.y, 1.65f) * Mathf.Rad2Deg);
        };
        Action<CityTradeKind> open = kind => { steps.Add(() => face(kind)); steps.Add(() => key(Key.E)); steps.Add(() => { key(null); verify(trade.MenuOpen, "E ray opens " + kind); }); };
        Action<string, string> submit = (name, parent) =>
        {
            steps.Add(() =>
            {
                var button = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name && (parent == null || b.transform.parent.name == parent));
                verify(button.interactable, "actionable UI " + (parent ?? name)); EventSystem.current.SetSelectedGameObject(button.gameObject);
            });
            steps.Add(() => key(Key.Enter)); steps.Add(() => key(null));
        };
        Action close = () => { steps.Add(() => key(Key.Escape)); steps.Add(() => { key(null); verify(!trade.MenuOpen, "Esc closes city menu"); }); };
        Action faceStation = () => player.RestorePose(t.transform.TransformPoint(new Vector3(0, .05f, -1.25f)), t.transform.eulerAngles.y, 22);
        steps.Add(() => verify(trade.SharpeningSite.PlaceAvailable(), "station volume and approach free"));
        // Real obstruction uses a temporary collider, never alters the authored city.
        steps.Add(() =>
        {
            var obstacle = new GameObject("M7 temporary occupancy check").AddComponent<BoxCollider>();
            obstacle.transform.position = t.transform.position + Vector3.up * .85f; obstacle.size = Vector3.one * .1f; Physics.SyncTransforms();
            verify(!trade.SharpeningSite.PlaceAvailable(), "physical occupied place refused"); UnityEngine.Object.Destroy(obstacle.gameObject);
        });
        open(CityTradeKind.Seller); submit("Купить", "Заточной станок");
        steps.Add(() => verify(save.Session.SharpeningStationOwned && t.gameObject.activeInHierarchy && save.Session.Balance == 105, "real shop installs M6 station once for $400"));
        submit("Купить", "Кухонный нож"); steps.Add(() => verify(save.Session.KnifePurchased && save.Session.KnifePlace == KnifeLocation.Pocket, "real shop buys knife to separate inventory")); close();
        steps.Add(() => key(Key.Tab)); steps.Add(() => { key(null); verify(trade.MenuOpen, "Tab opens map"); });
        submit("Инвентарь", null);
        steps.Add(() =>
        {
            var icon = UnityEngine.Object.FindObjectsByType<Image>().First(i => i.sprite != null && i.sprite.name.StartsWith("CityKnifeIcon") && i.GetComponentInParent<Button>() != null);
            EventSystem.current.SetSelectedGameObject(icon.GetComponentInParent<Button>().gameObject);
        });
        steps.Add(() => key(Key.Enter)); steps.Add(() => key(null));
        steps.Add(() =>
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name.StartsWith("Взять · кухонный нож"));
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        });
        steps.Add(() => key(Key.Enter)); steps.Add(() => { key(null); verify(save.Session.KnifeInHands && save.Session.InHands == null, "inventory equips exact knife in shared hands"); }); close();
        steps.Add(faceStation); steps.Add(() => key(Key.E));
        steps.Add(() => { key(null); verify(t.Active && !player.enabled && !trade.enabled, "world E enters city M6; world input suspended"); });
        steps.Add(() => pointer(true, Vector2.zero)); steps.Add(() => { verify(t.State.Placed, "LMB places city knife"); pointer(false, Vector2.zero); });
        steps.Add(() => key(Key.Space)); steps.Add(() => { key(null); verify(t.State.Running, "Space starts motor"); });
        steps.Add(() => { Set(t, "origin", new Vector3(0, 1.27f, .012f)); Set(t, "angle", 35f); pointer(true, new Vector2(10, 0)); });
        steps.Add(() => { verify(t.State.Progress == 0 && t.State.Contact, "real mouse wrong angle contacts without progress"); Set(t, "angle", 20f); pointer(true, new Vector2(10, 0)); });
        float partial = 0; int pattern = 0;
        steps.Add(() => { partial = t.State.Progress; pattern = save.Session.KnifeCoatingSeed; verify(partial > 0 && !t.State.Complete && t.EffectsPlaying, "real held mouse correct angle gives local progress and effects"); verify(save.Save() && t.State.Progress == partial && !t.State.Running && !t.EffectsPlaying, "explicit disk save during contact normalizes transient work without losing coverage"); pointer(false, Vector2.zero); });
        steps.Add(() => key(Key.R)); steps.Add(() => { key(null); verify(t.State.Progress == partial && save.Session.KnifeCoatingSeed == pattern, "city R cannot reset coverage or pattern"); });
        steps.Add(() => key(Key.P)); steps.Add(() => { key(null); verify(!t.Active && walk.IsPaused && !t.State.Running && !t.EffectsPlaying, "P exits work and opens city pause with effects off"); });
        double pausedAt = 0;
        steps.Add(() => pausedAt = save.Session.ElapsedSeconds);
        steps.Add(() => verify(save.Session.ElapsedSeconds == pausedAt, "pause stops active deadline clock"));
        steps.Add(() => key(Key.F5)); steps.Add(() => key(null)); steps.Add(() => key(Key.F9));
        steps.Add(() => { key(null); verify(save.Session.SharpeningState.Progress == partial && save.Session.KnifeCoatingSeed == pattern && !t.State.Running && walk.IsPaused, "F5/F9 preserve partial coverage/pattern; motor off"); });
        steps.Add(() => key(Key.Escape)); steps.Add(() => key(null)); steps.Add(faceStation); steps.Add(() => key(Key.E));
        steps.Add(() => { key(null); verify(t.Active && t.State.Progress == partial, "E continues loaded partial knife"); });
        steps.Add(() => key(Key.Space)); steps.Add(() => key(null));
        steps.Add(() => { t.SendMessage("OnApplicationFocus", false); verify(!t.Active && walk.IsPaused && !t.State.Running && !t.EffectsPlaying, "focus loss exits, pauses, clears motor/effects"); t.SendMessage("OnApplicationFocus", true); });
        steps.Add(() => key(Key.Escape)); steps.Add(() => key(null)); steps.Add(() => key(Key.E));
        steps.Add(() => { key(null); verify(save.Session.KnifeInHands && !t.Active, "E after departure retrieves unfinished knife physically"); });
        steps.Add(() => key(Key.G)); steps.Add(() => { key(null); verify(save.Session.KnifePlace == KnifeLocation.Pocket, "G stows partial knife"); });
        steps.Add(() => { verify(save.Session.EquipKnife(out _), "reequip same knife"); }); steps.Add(() => key(Key.E));
        steps.Add(() => { key(null); verify(t.Active && t.State.Progress == partial && !t.State.Running, "reentry preserves partial work with explicit motor restart required"); t.State.Place(); t.State.ToggleMotor(); Pass(t.State); Pass(t.State); });
        steps.Add(() => key(Key.F)); steps.Add(() => { key(null); verify(save.Session.KnifeReady && t.State.Complete, "real F on parked complete knife marks ready"); });
        steps.Add(() => key(Key.Escape)); steps.Add(() => { key(null); verify(!t.Active && player.enabled && trade.enabled && Mathf.Abs(eye.fieldOfView - fov) < .01f && !t.EffectsPlaying, "Esc restores walking camera/FOV and input"); });
        steps.Add(() => key(Key.E)); steps.Add(() => { key(null); verify(save.Session.KnifeInHands, "E retrieves finished knife"); });
        steps.Add(() => player.RestorePose(new Vector3(-66.8f, .08f, 30), 0, 60)); steps.Add(() => key(Key.Q));
        steps.Add(() => { key(null); verify(save.Session.KnifePlace == KnifeLocation.World, "real Q places knife on clear ground"); });
        steps.Add(() =>
        {
            var p = save.Session.KnifePosition; var h = 1.73f - p.y;
            player.RestorePose(new Vector3(p.x, .08f, p.z - 1), 0, Mathf.Atan2(h, 1) * Mathf.Rad2Deg);
        });
        steps.Add(() => key(Key.E)); steps.Add(() => { key(null); verify(save.Session.KnifeInHands, "real ray E picks up placed same knife"); });
        open(CityTradeKind.B); submit("Принять заточку", null); submit("Передать проверенный нож", null);
        steps.Add(() => verify(save.Session.Order(DealOrderId.Sharpen).State == DealOrderState.Completed && !save.Session.KnifeOwned && save.Session.Balance == 565, "buyer UI transfers exact ID and pays $500 once")); close();
        steps.Add(() => { verify(save.Save() && save.Load(false), "actual disk save/load after payment"); verify(!save.Session.DeliverKnife(new[] { save.Session.KnifeId }, out _), "post-load payout refused"); });
        foreach (var ready in new[] { false, true })
        {
            var finished = ready;
            steps.Add(() =>
            {
                var candidate = Prepared();
                if (finished) Finish(candidate);
                else { candidate.EndSharpening(); candidate.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _); }
                typeof(CitySaveController).GetMethod("Bind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new object[] { candidate, World() });
                walk.SetPaused(false);
            });
            open(CityTradeKind.Seller); submit("Продать вещи", null);
            submit("Продать нож · $" + (finished ? 60 : 20) + " · закрывает заказ заточки", null);
            steps.Add(() => verify(!save.Session.KnifeOwned && save.Session.Balance == (finished ? 125 : 85) && save.Session.Order(DealOrderId.Sharpen).State == DealOrderState.Unavailable, "real buyback UI settles once: " + finished));
            close();
        }
        var index = 0; var frame = Time.frameCount; var next = EditorApplication.timeSinceStartup; var deadline = next + 180;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner; running = false; if (t != null) t.Exit();
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); Application.runInBackground = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInput; InputSystem.settings.backgroundBehavior = oldFocus;
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("M7 interrupted/timeout step " + index);
                if (Time.frameCount < frame + 3 || EditorApplication.timeSinceStartup < next) return;
                if (index == steps.Count) { cleanup(); File.AppendAllText(InputLog, "INPUT PASS " + count + " assertions\n"); Debug.Log("M7 INPUT PASS " + count); return; }
                steps[index++](); frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .16;
            }
            catch (Exception e) { cleanup(); File.AppendAllText(InputLog, "INPUT FAIL step " + index + " " + e + "\n"); Debug.LogException(e); }
        };
        EditorApplication.update += runner;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
}
