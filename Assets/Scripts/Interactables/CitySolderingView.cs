using Inventory;
using UnityEngine;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        public Vector3 StationPosition => sceneBindings.Station.transform.position;
        private SolderingTrial solderingStation;
        public SolderingTrial SolderingStation => solderingStation;
        public void RefreshSolderingWorld() => Refresh();

        private void BuildSolderingStation()
        {
            solderingStation = sceneBindings.Station.Station;
            walk.SolderingStation = solderingStation;
        }

        public bool StationPlaceAvailable() => sceneBindings.Station.PlaceAvailable();

        private void RefreshSolderingStation()
        {
            sceneBindings.Station.Refresh(session.StationOwned, session.CanBuyStation);
        }

        private static string CityCondition(FlashlightUnit lamp) => lamp.InitialCondition == FlashlightCondition.ConnectionFault ?
            !lamp.IsTested ? "не проверен" : lamp.LastTestPassed ? "пайка проверена" : "дефект соединения" : FirstDealText.Condition(lamp);

        private void SolderingShop()
        {
            ProductCard("Паяльная станция", "Стол, паяльник, держатель, коврик и лоток · место отмечено в мастерской", "$300", lampIcon,
                () => Execute("station"), session.CanBuyStation && session.Balance >= 300 && StationPlaceAvailable(),
                session.CanBuyStation ? "Нужны $300 и свободное место" : "Продай всю первую партию", session.StationOwned);
            if (!session.StationOwned)
            {
                return;
            }
            ProductCard("Фонарик для пайки", "Один экземпляр · нужна диагностика соединения", "$40", lampIcon,
                () => Execute("solderlamp"), session.SolderingLamp == null && session.Balance >= 40 && session.InHands == null && !session.IsCrateCarried,
                session.Balance < 40 ? "Нужно $40" : "Освободи руки: убери фонарь или поставь ящик", session.SolderingLamp != null);
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
