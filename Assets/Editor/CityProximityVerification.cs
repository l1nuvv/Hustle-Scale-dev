using System;
using System.IO;
using System.Linq;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;

public static class CityProximityVerification
{
    [MenuItem("Tools/City/Verify Workshop Proximity In Play Mode")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("CityProximity"))
            throw new InvalidOperationException("Isolated CityProximity save required before entering Play Mode.");
        var report = new System.Text.StringBuilder(); var count = 0;
        Action<bool, string> check = (ok, text) =>
        {
            report.AppendLine((ok ? "PASS " : "FAIL ") + text);
            if (!ok) throw new InvalidOperationException(text);
            count++;
        };
        try
        {
            var vehicle = UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>();
            var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
            var trade = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>();
            var player = save.GetComponent<PlayerController>(); var body = vehicle.GetComponent<Rigidbody>();
            check(!vehicle.HasBlockingCityOverlap(), "loaded car clear of runtime yard");
            vehicle.RestoreCityPose(new Inventory.CityWorldData
            {
                HasVehiclePose = true,
                VehiclePosition = new Vector3(-72.93f, .01f, 17.01f), VehicleEuler = new Vector3(.01f, 182.2f, .6f)
            });
            check(vehicle.HasBlockingCityOverlap(), "reproduce saved car/container penetration");
            check(vehicle.EnsureClearCityPose() && !vehicle.HasBlockingCityOverlap(), "recover only obstructed car to clear parking");
            var parked = body.position;
            check(!vehicle.EnsureClearCityPose() && body.position == parked, "valid pose retained");
            check(save.Save() && save.Load(false) && !vehicle.HasBlockingCityOverlap(), "safe pose survives actual save/load");
            save.NewGame(); walk.SetPaused(false);
            player.RestorePose(new Vector3(-65, .08f, 25), 0, 0);
            var capsule = player.GetComponent<CharacterController>(); float distance = 0;
            foreach (var goal in new[] { new Vector3(-65, .08f, 14), new Vector3(-60, .08f, 14),
                new Vector3(-60, .08f, 8.35f), new Vector3(-53, .08f, 8.35f) })
            {
                for (var step = 0; step < 400; step++)
                {
                    var delta = Vector3.ProjectOnPlane(goal - player.transform.position, Vector3.up);
                    if (delta.magnitude < .08f) break;
                    var move = Vector3.ClampMagnitude(delta, .1f); capsule.Move(move); distance += move.magnitude;
                }
                check(Vector3.ProjectOnPlane(goal - player.transform.position, Vector3.up).magnitude < .12f, "walk clear waypoint " + goal);
            }
            check(distance < 30, "workshop-to-seller walking route under 30m; actual " + distance);
            player.RestorePose(player.transform.position, 0, 12);
            var seller = trade.Points.First(p => p.Kind == CityTradeKind.Seller);
            check(trade.Reachable(seller), "new seller interaction reachable through actual colliders");
            trade.Open(seller); check(trade.MenuOpen && trade.Execute("purchase"), "buy first lot at nearby counter");
            check(trade.Crate.position == CityTradeView.SellerDock, "purchased crate follows new seller dock");
            trade.CloseMenu(); check(save.Save() && save.Load(false), "purchase and world round trip");
            check(trade.Session.Balance == 120 && trade.Session.Units.Count == 3, "ordinary lot price and count preserved");
            report.AppendLine("PROXIMITY PASS " + count + "; Unity " + Application.unityVersion);
            Debug.Log("PROXIMITY PASS " + count);
        }
        catch (Exception e) { report.AppendLine(e.ToString()); throw; }
        finally { File.WriteAllText("Logs/Codex-city-proximity.log", report.ToString()); }
    }
}
