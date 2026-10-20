using System;
using System.IO;
using Inventory;
using Player.Movement;
using UnityEngine;

namespace Player
{
    // Lives independently of the walking overlay, which the existing vehicle disables while driving.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class CitySaveController : MonoBehaviour
    {
        private PlayerController player;
        private CityGreyboxWalk walk;
        private CityUtilityVehicle vehicle;
        private CitySaveStore store;
        private bool dirty, loading;
        public WorkshopSession Session { get; private set; }
        public CityWorldData World { get; private set; }
        public string Message { get; private set; } = "";
        public bool HasBackup => store != null && store.BackupExists;
        public bool IsDriving => vehicle != null && vehicle.IsDriving;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            walk = GetComponent<CityGreyboxWalk>();
            store = new CitySaveStore(Path.Combine(Application.persistentDataPath, "city-save.json"));
#if UNITY_EDITOR
            // The repeatable Play Mode fixture must never overwrite the player's real save.
            var verificationPath = UnityEditor.SessionState.GetString("HustleScale.S2SaveOverride", "");
            if (verificationPath.Length > 0) store = new CitySaveStore(verificationPath);
#endif
        }

        private void Start()
        {
            vehicle = FindAnyObjectByType<CityUtilityVehicle>();
            if (store.Exists) Load(false);
            else if (store.BackupExists)
            {
                Message = "Основной файл отсутствует. Можно загрузить резервную копию или начать новую игру.";
                walk.SetPaused(true);
            }
            else NewGame();
        }

        private void Update()
        {
            if (Session == null) return;
            Session.SetPaused(IsDriving ? vehicle.IsPaused : walk.IsPaused);
            Session.AdvanceActiveSeconds(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (dirty && !IsDriving) Save();
        }

        public void RequestSave() => dirty = true;

        private void Bind(WorkshopSession session, CityWorldData world)
        {
            if (Session != null) Session.TransactionCompleted -= RequestSave;
            Session = session;
            World = world;
            Session.TransactionCompleted += RequestSave;
            dirty = false;
        }

        public void NewGame()
        {
            if (IsDriving) { Message = "Сначала выйди из автомобиля."; return; }
            var world = new CityWorldData
            {
                PlayerPosition = new Vector3(-65, 0.08f, 25), PlayerYaw = 180,
                CrateDock = CityCrateDock.Seller, PlacedLamps = Array.Empty<CityPlacedLamp>()
            };
            Bind(WorkshopSession.CreateCity(), world);
            player.RestorePose(world.PlayerPosition, world.PlayerYaw, world.CameraPitch);
            loading = true;
            walk.SetPaused(true);
            loading = false;
            Save();
        }

        public bool Save()
        {
            dirty = false;
            if (loading || Session == null) return false;
            if (IsDriving) { Message = "Сначала выйди из автомобиля для сохранения."; return false; }
            World.PlayerPosition = transform.position;
            World.PlayerYaw = transform.eulerAngles.y;
            World.CameraPitch = player.CameraPitch;
            var success = store.TrySave(Session.CaptureCity(World), out var message);
            Message = message;
            return success;
        }

        public bool Load(bool backup)
        {
            if (IsDriving) { Message = "Сначала выйди из автомобиля для загрузки."; return false; }
            if (!store.TryLoad(backup, out var data, out var candidate, out var message))
            {
                Message = message + " Текущее состояние не изменено.";
                loading = true;
                walk.SetPaused(true);
                loading = false;
                return false;
            }
            Bind(candidate, data.World);
            player.RestorePose(World.PlayerPosition, World.PlayerYaw, World.CameraPitch);
            loading = true;
            walk.SetPaused(true);
            loading = false;
            Message = backup ? "Резервная копия загружена. Продолжи игру или сохрани её как основной файл." : message;
            return true;
        }

        public void SaveOnPause()
        {
            if (!loading && Session != null) Save();
        }

        private void OnDestroy()
        {
            if (Session != null) Session.TransactionCompleted -= RequestSave;
        }

        private void OnApplicationQuit()
        {
            if (!IsDriving) Save();
        }
    }
}
