using System;
using System.IO;
using System.Linq;
using Inventory;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CityCargoVerification
{
    private static int checks;

    [MenuItem("Tools/City/Verify Vehicle Cargo")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        checks = 0;
        var world = new CityWorldData
        {
            PlayerPosition = new Vector3(83, 0.1f, -35), CrateDock = CityCrateDock.Seller,
            PlacedLamps = Array.Empty<CityPlacedLamp>(), HasVehiclePose = true,
            VehiclePosition = new Vector3(82, 0.5f, -33), VehicleEuler = new Vector3(0, 90, 0)
        };
        var session = WorkshopSession.CreateCity();
        session.SetPaused(false);
        Check(!session.LoadVehicleCrate(out _), "cannot load before purchase");
        Check(session.PurchaseLot(out _), "purchase");
        var lamps = JsonUtility.ToJson(session.CaptureCity(world));
        Check(!session.LoadVehicleCrate(out _), "must carry crate to load");
        Check(lamps == JsonUtility.ToJson(session.CaptureCity(world)), "failed load is atomic");
        Check(session.ToggleUnit(0, out _), "hold incompatible lamp");
        var held = JsonUtility.ToJson(session.CaptureCity(world));
        Check(!session.LoadVehicleCrate(out _) && !session.ToggleCrate(out _), "lamp blocks loading and carrying");
        Check(held == JsonUtility.ToJson(session.CaptureCity(world)), "hands rejection atomic");
        Check(session.ToggleUnit(0, out _) && session.ToggleCrate(out _), "carry crate");
        var identities = string.Join(",", session.Units.Select(unit => unit.InstanceId));
        var balance = session.Balance;
        Check(session.LoadVehicleCrate(out _), "load");
        Check(session.IsCrateInVehicle && !session.IsCrateCarried && session.InHands == null, "exclusive crate state");
        var loaded = JsonUtility.ToJson(session.CaptureCity(world));
        Check(!session.LoadVehicleCrate(out _) && !session.ToggleCrate(out _) && !session.ToggleUnit(0, out _), "loaded crate cannot be duplicated or accessed remotely");
        Check(!session.SellBack(session.Units.Select(unit => unit.InstanceId).ToArray(), out _), "cargo is not physically presented for sale");
        Check(loaded == JsonUtility.ToJson(session.CaptureCity(world)), "repeat and remote failures atomic");
        session.SetPaused(true);
        Check(!session.UnloadVehicleCrate(out _), "pause blocks unload");
        session.SetPaused(false);
        world.VehiclePosition = new Vector3(-70, 0.5f, 30);
        world.VehicleEuler = new Vector3(0, 180, 0);
        var folder = Path.GetFullPath("Temp/CityCargo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var store = new CitySaveStore(Path.Combine(folder, "cargo.json"));
            Check(store.TrySave(session.CaptureCity(world), out _), "save cargo after trip");
            Check(store.TryLoad(false, out var data, out var restored, out _), "load cargo");
            Check(restored.IsCrateInVehicle && !restored.IsCrateCarried && restored.IsPaused, "loaded cargo restored paused");
            Check(data.World.VehiclePosition == world.VehiclePosition && data.World.VehicleEuler == world.VehicleEuler, "vehicle pose round trip");
            Check(identities == string.Join(",", restored.Units.Select(unit => unit.InstanceId)), "stable lamp identities");
            Check(restored.Balance == balance && restored.Units.All(unit => unit.Location == FlashlightLocation.Crate), "money and cargo conserved");
            var broken = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(data));
            broken.IsCrateCarried = true;
            Check(!WorkshopSession.TryRestoreCity(broken, out _, out _), "reject two crate locations");
            broken.IsCrateCarried = false;
            broken.World.HasVehiclePose = false;
            Check(!WorkshopSession.TryRestoreCity(broken, out _, out _), "reject cargo without vehicle pose");
            broken.World.HasVehiclePose = true;
            broken.World.VehicleEuler.y = float.NaN;
            Check(!WorkshopSession.TryRestoreCity(broken, out _, out _), "reject invalid pose");
            restored.SetPaused(false);
            Check(restored.UnloadVehicleCrate(out _), "unload");
            Check(!restored.UnloadVehicleCrate(out _), "no repeated unload");
            data.World.CrateDock = CityCrateDock.Workshop;
            Check(store.TrySave(restored.CaptureCity(data.World), out _) && store.TryLoad(false, out data, out restored, out _), "unloaded snapshot round trip");
            Check(!restored.IsCrateInVehicle && !restored.IsCrateCarried && data.World.CrateDock == CityCrateDock.Workshop, "unloaded location retained");
            restored.SetPaused(false);
            Check(restored.ToggleUnit(0, out _) && restored.StowHeld(out _) && restored.ToggleCrate(out _) && restored.LoadVehicleCrate(out _), "load remaining cargo with pocket lamp");
            Check(restored.EquipSlot(0, out _), "hold pocket lamp after loading");
            var occupied = JsonUtility.ToJson(restored.CaptureCity(data.World));
            Check(!restored.UnloadVehicleCrate(out _), "occupied hands block unload");
            Check(occupied == JsonUtility.ToJson(restored.CaptureCity(data.World)), "failed unload atomic");
        }
        finally { Directory.Delete(folder, true); }

        var legacy = WorkshopSession.CreateCity().CaptureCity(new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() });
        legacy.Version = 1;
        Check(WorkshopSession.TryRestoreCity(legacy, out _, out _), "existing v1 save remains readable");
        VerifyCargoSpace();
        S2Verification.Run();
        var result = "CITY CARGO PASS: " + checks + " assertions; Unity " + Application.unityVersion + "; EditMode only";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Codex-city-cargo.log", result + "\n");
        Debug.Log(result);
    }

    private static void VerifyCargoSpace()
    {
        const string path = "Assets/Scenes/CityGreybox.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        var alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var vehicle = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CityUtilityVehicle>(true)).Single();
            var floor = vehicle.transform.Find("Bed floor");
            var gate = vehicle.transform.Find("Tailgate");
            Check(floor != null && gate != null && gate.GetComponent<Collider>() != null, "actual vehicle bed and interaction collider");
            Check(floor.localScale.x >= 1.25f && floor.localScale.z >= 0.65f, "crate fits actual bed floor");
            var rearEdge = floor.localPosition.z - 0.325f;
            Check(rearEdge > gate.localPosition.z + gate.localScale.z / 2, "crate clears tailgate");
            var bulkhead = vehicle.transform.Find("Cab rear bulkhead");
            Check(floor.localPosition.z + 0.325f < bulkhead.localPosition.z - bulkhead.localScale.z / 2, "crate clears cab");
            foreach (Transform side in vehicle.transform)
                if (side.name == "Bed side")
                    Check(Mathf.Abs(side.localPosition.x) - side.localScale.x / 2 > 0.625f, "crate clears side wall");
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void Check(bool value, string context)
    {
        checks++;
        if (!value) throw new InvalidOperationException("City cargo: " + context);
    }
}
