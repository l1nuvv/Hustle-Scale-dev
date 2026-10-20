using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    // Explicit development-Player fixture; each phase runs in a separate process.
    public sealed class CityPlayerVerification : MonoBehaviour
    {
        public static string SavePath { get; private set; }
        private static string output, phase;
        private static bool hadSave;
        private CitySaveController save;
        private CityTradeView trade;
        private CityGreyboxWalk walk;
        private PlayerController player;
        private Camera eye;
        private string runtimeFailure;
        private readonly List<string> events = new List<string>();
        private readonly Report report = new Report();

        [Serializable]
        private sealed class Report
        {
            public string phase, result, unity, platform, failure;
            public int assertions;
            public CitySaveData initial, final;
            public string[] events;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SavePath = null; output = null; phase = null;
            if (!Debug.isDebugBuild || Application.isEditor) return;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith("-city-verify=", StringComparison.Ordinal)) output = Path.GetFullPath(argument.Substring(13));
                if (argument.StartsWith("-city-verify-phase=", StringComparison.Ordinal)) phase = argument.Substring(19);
            }
            if (output == null) return;
            SavePath = Path.Combine(output, "city-save.json");
            hadSave = File.Exists(SavePath);
            Directory.CreateDirectory(output);
            DontDestroyOnLoad(new GameObject("City development Player verification", typeof(CityPlayerVerification)));
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += RuntimeError;
            report.phase = phase; report.unity = Application.unityVersion; report.platform = Application.platform.ToString();
            yield return null; yield return null; yield return null;
            var started = Time.realtimeSinceStartupAsDouble;
            var routine = Scenario();
            while (true)
            {
                bool more;
                try
                {
                    Check(runtimeFailure == null, runtimeFailure ?? "no runtime exception");
                    Check(Time.realtimeSinceStartupAsDouble - started < 90, "phase timeout");
                    more = routine.MoveNext();
                }
                catch (Exception exception)
                {
                    Finish(exception.ToString());
                    yield break;
                }
                if (!more) break;
                yield return routine.Current;
            }
            Finish(null);
        }

        private void RuntimeError(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
                runtimeFailure = message + "\n" + stack;
        }

        private void OnDestroy() => Application.logMessageReceived -= RuntimeError;

        private void Check(bool condition, string context)
        {
            report.assertions++;
            if (!condition) throw new InvalidOperationException(context);
        }

        private void Finish(string failure)
        {
            report.result = failure == null ? "PASS" : "FAIL";
            report.failure = failure;
            report.events = events.ToArray();
            if (save != null && save.Session != null) report.final = save.Session.CaptureCity(save.World);
            File.WriteAllText(Path.Combine(output, (phase ?? "invalid") + "-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("CITY PLAYER " + phase + " " + report.result + ": " + report.assertions + " assertions " + failure);
            Application.Quit(failure == null ? 0 : 4);
        }

        private CityTradePoint Point(CityTradeKind kind) => trade.Points.First(p => p.Kind == kind);

        private void Open(CityTradeKind kind)
        {
            trade.CloseMenu(); walk.SetPaused(false);
            var point = Point(kind);
            var position = point.transform.position;
            var offset = kind == CityTradeKind.A || kind == CityTradeKind.B ? 2.1f : 1.5f;
            var feet = new Vector3(position.x, 0.08f, position.z - offset);
            var angles = Quaternion.LookRotation(point.GetComponent<Collider>().bounds.center - (feet + Vector3.up * 1.65f)).eulerAngles;
            player.RestorePose(feet, angles.y, Mathf.DeltaAngle(0, angles.x));
            Physics.SyncTransforms();
            Check(trade.Reachable(point), "reachable " + kind);
            Check(Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponent<CityTradePoint>() == point,
                "nearest interaction ray " + kind);
            point.Interact();
            Check(trade.MenuOpen && walk.IsPaused, "menu opened " + kind);
            events.Add("Open " + kind);
        }

        private void Command(string command, int index = -1)
        {
            Check(trade.Execute(command, index), "command " + command + ": " + index);
            events.Add("Command " + command + ": " + index);
        }

        private void Reject(string command, string context)
        {
            var before = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
            var visible = Enumerable.Range(0, 3).Select(i => trade.Lamp(i).gameObject.activeSelf).ToArray();
            Check(!trade.Execute(command), context + " rejected");
            Check(before == JsonUtility.ToJson(save.Session.CaptureCity(save.World)) &&
                visible.SequenceEqual(Enumerable.Range(0, 3).Select(i => trade.Lamp(i).gameObject.activeSelf)), context + " atomic");
            events.Add("Reject atomically: " + context);
        }

        private void SelectOnly(params int[] indices)
        {
            for (var index = 0; index < 3; index++)
            {
                var i = index;
                var button = SelectionButton(i);
                if (button.GetComponentInChildren<Text>().text.StartsWith("[x]") != indices.Contains(i))
                {
                    Check(button.interactable, "selection button enabled " + i);
                    button.onClick.Invoke();
                    // RebuildMenu deactivates and replaces every row during the click.
                    button = SelectionButton(i);
                }
                Check(button.GetComponentInChildren<Text>().text.StartsWith("[x]") == indices.Contains(i), "selection " + i);
            }
        }

        private Button SelectionButton(int index) => FindObjectsByType<Button>().First(b => b.gameObject.activeInHierarchy &&
            b.GetComponentInChildren<Text>().text.StartsWith("[") &&
            b.GetComponentInChildren<Text>().text.Contains("Фонарь " + (index + 1)));

        // Same waypoints and 10 cm collision sweeps as the existing Editor S3 fixture.
        // This does not measure walking speed, WASD input or human navigation.
        private void Route(string name, params float[] coordinates)
        {
            trade.CloseMenu(); walk.SetPaused(false);
            player.RestorePose(new Vector3(coordinates[0], 0.08f, coordinates[1]), 0, 0);
            var capsule = player.GetComponent<CharacterController>();
            float length = 0;
            for (var i = 2; i < coordinates.Length; i += 2)
            {
                var start = new Vector3(coordinates[i - 2], 0, coordinates[i - 1]);
                var end = new Vector3(coordinates[i], 0, coordinates[i + 1]);
                var delta = end - start; length += delta.magnitude;
                var yaw = Quaternion.LookRotation(delta).eulerAngles.y;
                player.transform.rotation = Quaternion.Euler(0, yaw, 0);
                for (var distance = 0f; distance < delta.magnitude; distance += 0.1f)
                {
                    capsule.Move(delta.normalized * Mathf.Min(0.1f, delta.magnitude - distance) + Vector3.down * 0.03f);
                    Physics.SyncTransforms();
                    if (save.Session.IsCrateCarried)
                    {
                        var center = player.transform.position + Vector3.up * 0.8f + delta.normalized * 1.1f;
                        Check(!Physics.CheckBox(center, new Vector3(0.625f, 0.175f, 0.325f),
                            Quaternion.Euler(0, yaw, 0), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), name + " crate clearance");
                    }
                }
                Check(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(end.x, end.z)) < 0.2f, name + " capsule reaches waypoint " + i);
            }
            events.Add("Collision route " + name + ": " + length.ToString("F1") + "m");
        }

        private void Bindings()
        {
            Check(trade.Session == save.Session && save.Session.Units.Select(u => u.InstanceId).Distinct().Count() == 3,
                "one shared session and three distinct identities");
            Check(FindObjectsByType<CityTradeView>().Length == 1 && trade.Points.Count(p => p.Kind == CityTradeKind.Lamp) == 3,
                "one trade view and exactly three lamp targets");
            Check(save.Session.IsCrateCarried ? trade.Crate.parent == eye.transform :
                Vector3.Distance(trade.Crate.position, save.World.CrateDock == CityCrateDock.Seller ?
                    CityTradeView.SellerDock : CityTradeView.WorkshopDock) < 0.001f, "crate parent/dock");
            for (var i = 0; i < 3; i++)
            {
                var unit = save.Session.Units[i];
                var lamp = trade.Lamp(i);
                Check(lamp.gameObject.activeSelf == (WorkshopSession.IsOwned(unit) || unit.Location == FlashlightLocation.Seller),
                    "lamp visibility " + i);
                if (unit.Location == FlashlightLocation.Crate) Check(lamp.parent == trade.Crate, "crate lamp parent " + i);
                if (unit.Location == FlashlightLocation.Bench) Check(lamp.parent.name == "City trade runtime", "bench parent " + i);
                Check(lamp.GetComponent<Collider>().enabled == (unit.Location == FlashlightLocation.World), "lamp collider " + i);
            }
        }

        private void Matches(CitySaveData expected)
        {
            var actual = save.Session.CaptureCity(save.World);
            Check(Math.Abs(expected.ElapsedSeconds - actual.ElapsedSeconds) < 1e-9, "elapsed restored without offline ticking");
            actual.ElapsedSeconds = expected.ElapsedSeconds;
            for (var i = 0; i < expected.Orders.Length; i++)
            {
                Check(Math.Abs(expected.Orders[i].DeadlineSeconds - actual.Orders[i].DeadlineSeconds) < 1e-9, "deadline restored " + i);
                actual.Orders[i].DeadlineSeconds = expected.Orders[i].DeadlineSeconds;
            }
            Check(JsonUtility.ToJson(actual) == JsonUtility.ToJson(expected), "entire persisted snapshot exact");
        }

        private IEnumerator Scenario()
        {
            Check(phase == "prepare" || phase == "continue" || phase == "restore", "known phase");
            Check(CitySliceProfile.SavePath == null, "performance harness disabled");
            Check(phase == "prepare" ? !hadSave && !File.Exists(SavePath + ".backup") : hadSave, "correct fresh/resume precondition");
            trade = FindAnyObjectByType<CityTradeView>();
            Check(trade != null, "CityGreybox trade present");
            save = trade.GetComponent<CitySaveController>(); walk = trade.GetComponent<CityGreyboxWalk>();
            player = trade.GetComponent<PlayerController>(); eye = player.GetComponentInChildren<Camera>();
            player.GetComponent<PlayerInput>().MouseInputEnabled = false;
            Check(save.Session != null && walk.IsPaused && save.Session.IsPaused, "startup is paused");
            report.initial = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(save.Session.CaptureCity(save.World)));
            Bindings();
            if (phase != "prepare")
            {
                var checkpoint = new CitySaveStore(Path.Combine(output, phase == "continue" ? "prepare-save.json" : "continue-save.json"));
                Check(checkpoint.TryLoad(false, out var expected, out _, out var error), "checkpoint readable: " + error);
                Matches(expected);
                var position = player.transform.position;
                yield return new WaitForSecondsRealtime(0.25f);
                Matches(expected);
                Check(Vector3.Distance(player.transform.position, position) < 0.001f, "paused startup has no player drift");
                Check(Vector3.Distance(position, expected.World.PlayerPosition) < 0.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, expected.World.PlayerYaw)) < 0.001f &&
                    Mathf.Abs(player.CameraPitch - expected.World.CameraPitch) < 0.001f, "player/camera restored");
                events.Add("New process restored checkpoint; paused time and player stable");
            }
            if (phase == "prepare")
            {
                Check(save.Session.Balance == 300 && !save.Session.LotPurchased, "fresh city balance and lot");
                Open(CityTradeKind.Seller); Command("purchase"); Reject("purchase", "duplicate purchase"); Command("battery");
                Check(save.Session.Balance == 70 && save.Session.Expenses == 230 && save.Session.SpareBatteries == 1, "purchase accounting");
                Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu(); Bindings();
                Route("seller to workshop carrying", 83, -37, 80, -37, 80, -20, 65, 0, 35, -7, -15, -7, -42, 0, -65, 0, -65, 25, -69, 30);
                Open(CityTradeKind.WorkshopDock); Command("crate");
                Open(CityTradeKind.Crate); Command("unit", 1);
                Open(CityTradeKind.Workshop); Command("bench"); Command("test");
                Check(!save.Session.Units[1].LastTestPassed, "battery fault diagnosed");
                Command("repair");
                Check(save.Session.SpareBatteries == 0 && save.Session.RepairSeconds == 240 && !save.Session.Units[1].IsTested &&
                    save.Session.Units[1].Condition == FlashlightCondition.Working, "repair consumes one battery and requires retest");
            }
            else if (phase == "continue")
            {
                Check(save.Session.Balance == 70 && save.Session.RepairSeconds == 240 && save.Session.OnBench == save.Session.Units[1] &&
                    !save.Session.Units[1].IsTested, "mid-route repair survived process restart");
                Open(CityTradeKind.Workshop); Command("test"); Command("bench");
                Open(CityTradeKind.Crate); Command("unit", 1);
                foreach (var index in new[] { 0, 2 })
                {
                    Open(CityTradeKind.Crate); Command("unit", index);
                    Open(CityTradeKind.Workshop); Command("bench"); Command("test");
                    Check(save.Session.Units[index].LastTestPassed, "working lamp tested " + index);
                    Command("bench"); Open(CityTradeKind.Crate); Command("unit", index);
                }
                Command("crate"); trade.CloseMenu(); Bindings();
                Route("workshop to B carrying", -69, 30, -65, 25, -65, 0, -42, 0, -15, -7, -7, -7, -7, 8.4f);
                Open(CityTradeKind.B); Command("accept"); SelectOnly(0); Command("deliver");
                Check(save.Session.Balance == 195 && save.Session.OwnedLampCount == 2, "B pays exactly 125 for one lamp");
                Reject("deliver", "duplicate B payout");
                Route("B to A carrying", -7, 8.4f, -7, 5, -29, 5, -29, 8.4f);
                Open(CityTradeKind.A); Command("accept"); SelectOnly(1, 2); Command("deliver"); Reject("deliver", "duplicate A payout");
            }
            else
            {
                Paid();
                Open(CityTradeKind.A); Reject("deliver", "A payout after restart");
                Open(CityTradeKind.B); Reject("deliver", "B payout after restart");
                Open(CityTradeKind.Seller); Reject("purchase", "lot purchase after restart");
            }
            trade.CloseMenu(); walk.SetPaused(true);
            Check(save.Save(), "explicit checkpoint save");
            var snapshot = save.Session.CaptureCity(save.World);
            for (var i = 0; i < 3; i++)
            {
                Check(save.Load(false), "repeat load " + i);
                Matches(snapshot); Bindings();
            }
            if (phase != "prepare") Paid();
            Check(runtimeFailure == null, runtimeFailure ?? "no runtime errors");
            events.Add("Checkpoint saved; three repeated loads preserve entire snapshot and bindings");
        }

        private void Paid()
        {
            Check(save.Session.Balance == 455 && save.Session.Revenue == 385 && save.Session.Expenses == 230 &&
                save.Session.OwnedLampCount == 0 && save.Session.SpareBatteries == 0 && save.Session.RepairSeconds == 240,
                "final accounting: 300 - 180 - 50 + 125 + 260 = 455; zero owned lamps");
            Check(save.Session.Order(DealOrderId.A).State == DealOrderState.Completed &&
                save.Session.Order(DealOrderId.B).State == DealOrderState.Completed, "both orders completed");
        }
    }
}
