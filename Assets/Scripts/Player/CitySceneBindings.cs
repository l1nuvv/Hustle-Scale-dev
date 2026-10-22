using System.Collections.Generic;
using Interactables;
using Player.Movement;
using UnityEngine;

namespace Player
{
    // One explicit composition root per city scene; no economy or runtime registration framework.
    [DefaultExecutionOrder(-300)]
    public sealed class CitySceneBindings : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera walkingCamera;
        [SerializeField] private CitySaveController save;
        [SerializeField] private CityGreyboxWalk walk;
        [SerializeField] private CityTradeView trade;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Rect mapBounds = new Rect(-105, -70, 220, 135);
        public Rect MapBounds => mapBounds;
        public PlayerController Player => player;
        public Camera WalkingCamera => walkingCamera;
        public CitySaveController Save => save;
        public CityGreyboxWalk Walk => walk;
        public CityTradeView Trade => trade;
        public Transform PlayerSpawn => playerSpawn;
        public CityUtilityVehicle Vehicle { get; private set; }
        public CityGameplaySite Seller { get; private set; }
        public CityGameplaySite Workshop { get; private set; }
        public CityStationSite Station { get; private set; }
        public List<CityTradePoint> Points { get; } = new List<CityTradePoint>();
        public List<CityGameplaySite> Sites { get; } = new List<CityGameplaySite>();
        public bool Ready { get; private set; }

        private void Awake()
        {
            Ready = ValidateConfiguration();
            if (!Ready) { if (trade != null) trade.enabled = false; if (save != null) save.enabled = false; return; }
            Vehicle.ConfigureScene(player, walk, walkingCamera);
            // Defer station Awake until Save.Start has created/restored its session.
            Station.Station.gameObject.SetActive(false);
            Station.Station.ConfigureCity(save, walk, trade, player, walkingCamera);
        }

        public bool ValidateConfiguration()
        {
            var valid = true;
            if (player == null || walkingCamera == null || save == null || walk == null || trade == null || playerSpawn == null ||
                save.gameObject != player.gameObject || walk.gameObject != player.gameObject || trade.gameObject != player.gameObject ||
                !walkingCamera.transform.IsChildOf(player.transform) || mapBounds.width <= 0 || mapBounds.height <= 0 ||
                save.SceneBindings != this || !trade.HasSceneConfiguration(this) || player.GetComponent<PlayerInput>() == null)
            { Error("Assign Player, Walking Camera, Save, Walk, Trade, Player Spawn and positive Map Bounds. Player adapters must share the Player object and point to this Scene Bindings. Trade needs a Crate Prefab with Collider; Walk needs Worklight Prefab; Player needs PlayerInput."); return false; }
            Points.Clear(); Sites.Clear();
            var vehicles = new List<CityUtilityVehicle>();
            var stations = new List<CityStationSite>();
            var contexts = 0;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                contexts += root.GetComponentsInChildren<CitySceneBindings>(true).Length;
                Points.AddRange(root.GetComponentsInChildren<CityTradePoint>(true));
                Sites.AddRange(root.GetComponentsInChildren<CityGameplaySite>(true));
                vehicles.AddRange(root.GetComponentsInChildren<CityUtilityVehicle>(true));
                stations.AddRange(root.GetComponentsInChildren<CityStationSite>(true));
            }
            if (contexts != 1) { Error("Exactly one CitySceneBindings is required per scene."); valid = false; }
            Seller = null; Workshop = null;
            foreach (var site in Sites)
            {
                valid &= site.ValidateConfiguration();
                if (site.Role == CityTradeKind.Seller)
                { if (Seller != null) { Error("Duplicate Seller role: " + Seller.name + " / " + site.name); valid = false; } Seller = site; }
                if (site.Role == CityTradeKind.Workshop)
                { if (Workshop != null) { Error("Duplicate Workshop role: " + Workshop.name + " / " + site.name); valid = false; } Workshop = site; }
            }
            if (Seller == null || Workshop == null || vehicles.Count != 1 || stations.Count != 1)
            { Error("Require one Seller, one Workshop, one CityStationSite and one CityUtilityVehicle. Existing save slots represent one of each."); valid = false; }
            Vehicle = vehicles.Count == 1 ? vehicles[0] : null;
            Station = stations.Count == 1 ? stations[0] : null;
            if (Vehicle != null)
            {
                valid &= Vehicle.ValidateConfiguration();
                if (!Vehicle.gameObject.activeInHierarchy || !Vehicle.enabled) { Error("Vehicle must be active and enabled: " + Vehicle.name); valid = false; }
            }
            if (Station != null)
            {
                valid &= Station.ValidateConfiguration();
                if (!Station.gameObject.activeInHierarchy) { Error("Station site root must be active: " + Station.name); valid = false; }
            }
            foreach (var point in Points)
                if (point.GetComponent<Collider>() == null) { Debug.LogError($"City point '{point.name}': Collider is missing.", point); valid = false; }
            foreach (var role in new[] { CityTradeKind.Seller, CityTradeKind.Battery, CityTradeKind.Buyback, CityTradeKind.A, CityTradeKind.B, CityTradeKind.Workshop })
                if (!Points.Exists(point => point.Kind == role)) { Error("Missing interaction role: " + role); valid = false; }
            return valid;
        }

        private void Error(string reason) => Debug.LogError($"City scene '{name}': {reason}", this);
    }
}
