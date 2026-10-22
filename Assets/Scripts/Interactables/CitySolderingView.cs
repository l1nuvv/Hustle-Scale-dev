using Inventory;
using UnityEngine;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        public static readonly Vector3 StationPosition = new Vector3(-69, 0, 34);
        private SolderingTrial solderingStation;
        private Transform stationPreview;
        private Transform solderingPlaque;
        public SolderingTrial SolderingStation => solderingStation;
        public void RefreshSolderingWorld() => Refresh();

        private void BuildSolderingStation()
        {
            if (walk.SolderingStationPrefab == null) throw new System.InvalidOperationException("M5 station prefab missing on CityGreyboxWalk.");
            var station = Instantiate(walk.SolderingStationPrefab, root);
            station.transform.SetPositionAndRotation(StationPosition, Quaternion.identity);
            solderingStation = station.GetComponent<SolderingTrial>();
            solderingStation.ConfigureCity(save, walk, this, player, eye);
            walk.SolderingStation = solderingStation;
            stationPreview = new GameObject("Силуэт станции · $300").transform;
            stationPreview.SetParent(root, false); stationPreview.position = StationPosition;
            var outline = stationPreview.gameObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false; outline.sharedMaterial = accentMaterial; outline.widthMultiplier = .018f;
            var corners = new[] { new Vector3(-.475f, .94f, -.35f), new Vector3(.475f, .94f, -.35f),
                new Vector3(.475f, .94f, .35f), new Vector3(-.475f, .94f, .35f) };
            var silhouette = new System.Collections.Generic.List<Vector3>();
            foreach (var corner in corners) { silhouette.Add(corner); silhouette.Add(new Vector3(corner.x, .05f, corner.z)); silhouette.Add(corner); }
            silhouette.Add(corners[0]); outline.positionCount = silhouette.Count; outline.SetPositions(silhouette.ToArray());
            solderingPlaque = Board("ПАЙКА", StationPosition + new Vector3(0, 1.65f, .30f), 180, new Vector2(1.2f, .22f));
        }

        public bool StationPlaceAvailable()
        {
            foreach (var hit in Physics.OverlapBox(StationPosition + Vector3.up * .54f,
                new Vector3(.49f, .44f, .36f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (solderingStation == null || !hit.transform.IsChildOf(solderingStation.transform)) return false;
            return true;
        }

        private void RefreshSolderingStation()
        {
            if (solderingStation == null) return;
            solderingStation.gameObject.SetActive(session.StationOwned);
            stationPreview.gameObject.SetActive(session.CanBuyStation);
            solderingPlaque.gameObject.SetActive(session.StationOwned);
        }

        private static string CityCondition(FlashlightUnit lamp) => lamp.InitialCondition == FlashlightCondition.ConnectionFault ?
            !lamp.IsTested ? "не проверен" : lamp.LastTestPassed ? "пайка проверена" : "дефект соединения" : FirstDealText.Condition(lamp);

        private void SolderingShop()
        {
            if (!session.StationOwned)
            {
                ProductCard("Паяльная станция", "Стол, паяльник, держатель, коврик и лоток · место отмечено в мастерской", "$300", lampIcon,
                    () => Execute("station"), session.CanBuyStation && session.Balance >= 300 && StationPlaceAvailable(),
                    session.CanBuyStation ? "Нужны $300 и свободное место" : "Продай всю первую партию");
                return;
            }
            ProductCard("Фонарик для пайки", "Один экземпляр · нужна диагностика соединения", "$40", lampIcon,
                () => Execute("solderlamp"), session.SolderingLamp == null && session.Balance >= 40 && session.InHands == null && !session.IsCrateCarried);
            ProductCard("Припой", "Одна ремонтная доза · запас: " + session.SolderDoses, "$5", batteryIcon,
                () => Execute("dose"), session.Balance >= 5);
        }

        private void SolderingOffer()
        {
            if (!session.StationOwned) return;
            var order = session.Order(DealOrderId.Solder);
            Line("Пайка: один фонарик · $400 · " + (order.State == DealOrderState.Accepted ?
                WorkshopView.Clock(System.Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds)) : "18:00 с принятия"));
            if (order.State == DealOrderState.Available) Button("Принять пайку", () => Execute("solderaccept"));
            if (order.State == DealOrderState.Available || order.State == DealOrderState.Accepted)
                Button(order.State == DealOrderState.Accepted ? "Отменить пайку навсегда" : "Отказаться от пайки", () => Execute("solderclose"));
            if (order.State == DealOrderState.Accepted)
                Button("Передать восстановленный фонарик", () => Execute("solderdeliver"), Presented(3) && session.SolderingReady);
            else Line("Пайка: " + State(order.State));
        }
    }
}
