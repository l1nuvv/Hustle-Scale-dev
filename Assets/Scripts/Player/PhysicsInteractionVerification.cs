using System;
using System.Collections;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

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
            yield return null;
        }
    }
}
