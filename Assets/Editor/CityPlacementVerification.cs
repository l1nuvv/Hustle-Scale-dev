using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player;
using UnityEditor;
using UnityEngine;

public static partial class M5SolderingVerification
{
    // Exercise the real adapter at authored and relocated/rotated poses, with an isolated save.
    [MenuItem("Tools/City/Verify Prefab Placement In Play Mode")]
    public static void VerifyPlacement()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("CityPlacement"))
            throw new InvalidOperationException("Enter CityGreybox with an isolated CityPlacement save override.");
        report.Clear(); checks = 0;
        var context = UnityEngine.Object.FindAnyObjectByType<CitySceneBindings>();
        var trade = context.Trade; var save = context.Save; var walk = context.Walk;
        var vehicle = context.Vehicle;
        var moved = context.Sites.Select(site => site.transform).Concat(new[] { context.Station.transform }).ToArray();
        var positions = moved.Select(t => t.position).ToArray();
        var rotations = moved.Select(t => t.rotation).ToArray();
        var vehiclePosition = vehicle.transform.position; var vehicleRotation = vehicle.transform.rotation;
        GameObject platform = null;
        try
        {
            Check(context.Ready && context.ValidateConfiguration(), "scene dependencies valid");
            Check(trade.Points.Count(p => p.Kind == CityTradeKind.Workshop) == 2 &&
                trade.Points.Count(p => p.Kind == CityTradeKind.Seller) == 2, "only prefab targets; no duplicate runtime fixtures");
            for (var layout = 0; layout < 2; layout++)
            {
                if (layout == 1)
                {
                    platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    platform.name = "Placement verification temporary ground";
                    platform.transform.position = new Vector3(275, 19.9f, 250);
                    platform.transform.localScale = new Vector3(180, .2f, 110);
                    for (var i = 0; i < moved.Length; i++)
                        moved[i].SetPositionAndRotation(new Vector3(210 + i * 20, positions[i].y + 20, 230), Quaternion.Euler(0, 73, 0));
                    vehicle.RestoreCityPose(new CityWorldData { HasVehiclePose = true, VehiclePosition = new Vector3(275, 20.4f, 270), VehicleEuler = new Vector3(0, 117, 0) });
                    Physics.SyncTransforms();
                    Check(moved.All(t => Quaternion.Angle(t.rotation, Quaternion.identity) > 70), "all sites translated and rotated");
                }
                var ground = layout == 0 ? .08f : 20.08f;
                save.NewGame(); walk.SetPaused(false);
                // New game restores vehicle to its authored parking. Move it back to the test platform for layout 1.
                if (layout == 1) vehicle.RestoreCityPose(new CityWorldData { HasVehiclePose = true, VehiclePosition = new Vector3(275, 20.4f, 270), VehicleEuler = new Vector3(0, 117, 0) });
                var seller = PlacementFace(context, CityTradeKind.Seller, ground);
                seller.Interact(); Check(trade.MenuOpen, "seller reachable in layout " + layout);
                var balance = save.Session.Balance;
                Must(trade.Execute("purchase"));
                Check(save.Session.Balance < balance && trade.Session == save.Session, "purchase uses existing shared session");
                trade.CloseMenu();

                var session = WorkshopSession.CreateCity(19); session.SetPaused(false); Must(session.PurchaseLot(out _));
                PlacementBind(context, session);
                Check(Vector3.Distance(trade.Crate.position, context.Seller.CrateDock.position) < .001f &&
                    trade.Crate.parent == context.Seller.CrateDock, "crate follows seller local dock");
                var fault = Enumerable.Range(0, 3).First(i => session.Units[i].Condition == FlashlightCondition.BatteryFault);
                Must(session.ToggleUnit(fault, out _));
                var bench = PlacementFace(context, CityTradeKind.Workshop, ground);
                Must(trade.WorkshopAction(bench, "bench"));
                Check(trade.Lamp(fault).parent == context.Workshop.BenchItem && trade.Lamp(fault).localPosition == Vector3.zero &&
                    Quaternion.Angle(trade.Lamp(fault).rotation, context.Workshop.BenchItem.rotation) < .01f, "bench item follows translated/rotated anchor");
                Must(trade.WorkshopAction(PlacementFace(context, CityTradeKind.Workshop, ground), "test"));
                Check(session.OnBench.IsTested && !session.OnBench.LastTestPassed, "real bench diagnosis");
                PlacementFace(context, CityTradeKind.Battery, ground).Interact(); Must(trade.Execute("battery")); trade.CloseMenu();
                Must(trade.WorkshopAction(PlacementFace(context, CityTradeKind.Workshop, ground), "repair"));
                Must(trade.WorkshopAction(PlacementFace(context, CityTradeKind.Workshop, ground), "test"));
                Check(session.OnBench.LastTestPassed, "battery repair and retest");
                Must(session.ToggleBench(out _)); Must(session.ToggleUnit(fault, out _));
                Must(save.Save()); Must(save.Load(false)); walk.SetPaused(false);
                session = save.Session;
                Check(trade.Session == session && Vector3.Distance(trade.Crate.position, context.Seller.CrateDock.position) < .001f, "save/load retains shared session and local dock");
                Must(session.ToggleCrate(out _));
                PlacementFace(context, CityTradeKind.Cargo, ground).Interact();
                if (trade.MenuOpen) { Must(trade.Execute("crate")); trade.CloseMenu(); }
                Check(session.IsCrateInVehicle && trade.Crate.parent == vehicle.CargoAnchor && trade.Crate.localPosition == Vector3.zero, "rotated tailgate loads into cargo anchor");
                Must(save.Save()); Must(save.Load(false)); walk.SetPaused(false); session = save.Session;
                Check(session.IsCrateInVehicle && Vector3.Distance(trade.Crate.position, vehicle.CargoAnchor.position) < .001f, "cargo pose round trip");
                PlacementFace(context, CityTradeKind.Cargo, ground).Interact();
                if (trade.MenuOpen) { Must(trade.Execute("crate")); trade.CloseMenu(); }
                Check(session.IsCrateCarried && !session.IsCrateInVehicle, "tailgate unloads into same session");
                Must(session.ToggleCrate(out _));
                var door = vehicle.transform.Find("Driver Door");
                var approach = door.position - vehicle.transform.right;
                context.Player.RestorePose(new Vector3(approach.x, ground, approach.z), vehicle.transform.eulerAngles.y + 90, 0);
                Physics.SyncTransforms(); Must(vehicle.TryEnter());
                Check(!save.Save() && !save.Load(false), "driving still blocks save/load");
                Must(vehicle.TryExit());
                Check(Vector3.Distance(context.Player.transform.position, vehicle.transform.position) < 3, "exit uses local candidates");

                PlacementBind(context, Funded());
                Check(trade.StationPlaceAvailable(), "rotated station footprint is clear");
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.transform.position = context.Station.transform.TransformPoint(new Vector3(0, .54f, 0));
                obstacle.transform.localScale = Vector3.one * .2f;
                Physics.SyncTransforms();
                Check(!trade.StationPlaceAvailable(), "local purchase volume detects obstacle");
                PlacementFace(context, CityTradeKind.Seller, ground).Interact(); balance = save.Session.Balance;
                Check(!trade.Execute("station") && save.Session.Balance == balance, "blocked station purchase preserves money");
                UnityEngine.Object.DestroyImmediate(obstacle); Physics.SyncTransforms();
                Must(trade.Execute("station")); trade.CloseMenu();
                Check(trade.SolderingStation.gameObject.activeInHierarchy && trade.SolderingStation.IsCityOperation, "purchased prefab station activates city adapter");
                var prepared = Prepared(); PlacementBind(context, prepared);
                var station = trade.SolderingStation;
                context.Player.RestorePose(station.transform.TransformPoint(new Vector3(0, .08f, -1.4f)), station.transform.eulerAngles.y, 26);
                station.Interact(); Check(station.IsActive && station.Repair == prepared.RepairState, "relocated station uses shared repair state");
                Open(prepared); Solder(prepared, 1); station.Exit();
                var progress = prepared.SolderingState.Progress; var doses = prepared.SolderDoses;
                Must(save.Save()); Must(save.Load(false));
                Check(station.State.Progress == progress && save.Session.SolderDoses == doses && station.State.Heat == 0, "partial station work survives actual save/load");
                Check(Vector3.Distance(station.transform.position, context.Station.transform.position) < .001f, "load preserves authored station placement");
                report.AppendLine("LAYOUT " + layout + " PASS");
            }
            report.AppendLine("PLACEMENT PASS " + checks);
        }
        catch (Exception e) { report.AppendLine("PLACEMENT FAIL " + e); throw; }
        finally
        {
            trade.SolderingStation.Exit();
            for (var i = 0; i < moved.Length; i++) moved[i].SetPositionAndRotation(positions[i], rotations[i]);
            vehicle.RestoreCityPose(new CityWorldData { HasVehiclePose = true, VehiclePosition = vehiclePosition, VehicleEuler = vehicleRotation.eulerAngles });
            if (platform != null) UnityEngine.Object.DestroyImmediate(platform);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-city-placement.log", report.ToString());
        }
    }

    [MenuItem("Tools/City/Verify Legacy Saves With Prefabs In Play Mode")]
    public static void VerifyLegacyPlacement()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("CityPlacement"))
            throw new InvalidOperationException("An isolated CityPlacement Play Mode save is required.");
        report.Clear(); checks = 0;
        var context = UnityEngine.Object.FindAnyObjectByType<CitySceneBindings>();
        var store = (CitySaveStore)typeof(CitySaveController).GetField("store", Private).GetValue(context.Save);
        if (!store.Path.StartsWith(Path.GetFullPath("Temp/CityPlacement"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Controller must use the isolated Temp/CityPlacement save.");
        try
        {
            for (var version = 1; version <= 5; version++)
            {
                var session = WorkshopSession.CreateCity(41); session.SetPaused(false); Must(session.PurchaseLot(out _));
                var world = new CityWorldData
                {
                    PlayerPosition = context.PlayerSpawn.position, PlayerYaw = context.PlayerSpawn.eulerAngles.y,
                    CrateDock = CityCrateDock.Workshop, PlacedLamps = Array.Empty<CityPlacedLamp>(),
                    HasVehiclePose = version >= 2, VehiclePosition = context.Vehicle.ParkingPosition, VehicleEuler = context.Vehicle.ParkingRotation.eulerAngles
                };
                var data = session.CaptureCity(world); data.Version = version; data.HasTransportedCrate = true;
                // Historical v1/v2 fixture composition, also valid in v3-v5. No live economy mutation.
                for (var i = 0; i < 3; i++)
                {
                    data.Lamps[i].Condition = data.Lamps[i].InitialCondition = i == 1 ? FlashlightCondition.BatteryFault : FlashlightCondition.Working;
                    data.Lamps[i].Location = i == 0 ? FlashlightLocation.Bench : FlashlightLocation.Crate;
                }
                if (version < 5) data.Soldering = null;
                Must(store.TrySave(data, out _)); Must(context.Save.Load(false));
                Check(context.Trade.Session == context.Save.Session && context.Save.Session.Balance == data.Balance, "v" + version + " loads same economy");
                Check(context.Trade.Lamp(0).parent == context.Workshop.BenchItem && context.Trade.Crate.parent == context.Workshop.CrateDock,
                    "v" + version + " maps semantic bench/dock to local anchors");
                Check(context.Save.Session.Units.Select(l => l.InstanceId).SequenceEqual(data.Lamps.Select(l => l.InstanceId)), "v" + version + " retains item IDs");
                Must(context.Save.Save()); Must(context.Save.Load(false));
                Check(context.Trade.Lamp(0).parent == context.Workshop.BenchItem, "v" + version + " upgraded round trip");
            }
            var prepared = Prepared(); PlacementBind(context, prepared); Open(prepared); Solder(prepared, 1); context.Trade.SolderingStation.Exit();
            var previous = prepared.CaptureCity(context.Save.World); previous.Version = 5;
            Must(store.TrySave(previous, out _)); Must(context.Save.Load(false));
            Check(context.Save.Session.StationOwned && context.Trade.SolderingStation.State.Progress == prepared.SolderingState.Progress,
                "v5 partial soldering binds existing station prefab");
            report.AppendLine("LEGACY PREFABS PASS " + checks);
        }
        catch (Exception e) { report.AppendLine("LEGACY PREFABS FAIL " + e); throw; }
        finally { File.WriteAllText("Logs/Codex-city-prefabs-legacy.log", report.ToString()); }
    }

    private static void PlacementBind(CitySceneBindings context, WorkshopSession session)
    {
        var world = new CityWorldData
        {
            PlayerPosition = context.Player.transform.position, PlayerYaw = context.Player.transform.eulerAngles.y,
            PlacedLamps = Array.Empty<CityPlacedLamp>(), CrateDock = CityCrateDock.Seller,
            HasVehiclePose = true, VehiclePosition = context.Vehicle.transform.position, VehicleEuler = context.Vehicle.transform.eulerAngles
        };
        typeof(CitySaveController).GetMethod("Bind", Private).Invoke(context.Save, new object[] { session, world });
        context.Walk.SetPaused(false);
    }

    private static CityTradePoint PlacementFace(CitySceneBindings context, CityTradeKind kind, float ground)
    {
        Physics.SyncTransforms();
        foreach (var point in context.Trade.Points.Where(p => p.Kind == kind))
        {
            var center = point.GetComponent<Collider>().bounds.center;
            for (var angle = 0; angle < 360; angle += 30)
            {
                var offset = Quaternion.Euler(0, angle, 0) * Vector3.back * 1.6f;
                var feet = new Vector3(center.x + offset.x, ground, center.z + offset.z);
                var direction = center - (feet + Vector3.up * 1.65f);
                var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                context.Player.RestorePose(feet, yaw, pitch); Physics.SyncTransforms();
                if (context.Trade.Reachable(point)) return point;
                if (kind == CityTradeKind.Cargo)
                {
                    var eye = context.WalkingCamera.transform.position;
                    Physics.Linecast(eye, center, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    report.AppendLine($"Cargo probe {angle}: eye={eye}, target={center}, collider={point.GetComponent<Collider>().bounds.center}, first={hit.collider?.name}, enabled={point.GetComponent<Collider>().enabled}, view={point.View == context.Trade}");
                }
            }
        }
        throw new InvalidOperationException("Cannot reach " + kind + " at relocated pose.");
    }
}
