using System;
using Inventory;
using UnityEngine;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        [SerializeField] private CitySharpeningSite sharpeningSite;
        private Transform cityKnife;
        private Sprite knifeIcon;
        public SharpeningTrial SharpeningStation => sharpeningSite != null ? sharpeningSite.Station : null;
        public CitySharpeningSite SharpeningSite => sharpeningSite;
        public void ShowSharpeningMessage(string text) { message = text; }
        private void BuildSharpening()
        {
            if (sharpeningSite == null) throw new InvalidOperationException("Assign City Sharpening Site.");
            sharpeningSite.Station.ConfigureCity(save, walk, this, player, eye);
            walk.SharpeningStation = sharpeningSite.Station;
            cityKnife = Instantiate(sharpeningSite.KnifePrefab, root).transform;
            cityKnife.name = "Городской кухонный нож";
            var box = cityKnife.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(.07f, 0, 0); box.size = new Vector3(.46f, .045f, .08f);
            AddPoint(cityKnife, CityTradeKind.Knife);
            knifeIcon = Resources.Load<Sprite>("CityKnifeIcon");
        }
        private void RefreshSharpening()
        {
            if (sharpeningSite == null || cityKnife == null) return;
            sharpeningSite.Refresh(session.SharpeningStationOwned, session.CanBuySharpeningStation);
            cityKnife.gameObject.SetActive(session.KnifeOwned && (session.KnifeInHands || session.KnifePlace == KnifeLocation.World));
            cityKnife.GetComponent<Collider>().enabled = session.KnifePlace == KnifeLocation.World;
            cityKnife.SetParent(session.KnifeInHands ? eye.transform : root, false);
            if (session.KnifeInHands)
            {
                cityKnife.localPosition = new Vector3(.26f, -.26f, .65f);
                cityKnife.localRotation = Quaternion.Euler(0, -65, 0);
            }
            else cityKnife.SetPositionAndRotation(session.KnifePosition, Quaternion.Euler(0, session.KnifeYaw, 0));
            // Coating is data-driven in work mode; carry model shows the same physical knife.
        }
        private void SharpeningShop()
        {
            ProductCard("Заточной станок", "Комплект M6 · после продажи первой партии · независимо от пайки", "$400", knifeIcon,
                () => Execute("sharpstation"), session.CanBuySharpeningStation && session.Balance >= 400 && sharpeningSite.PlaceAvailable(),
                session.CanBuySharpeningStation ? "Нужны $400 и свободное место" : "Продай всю первую партию", session.SharpeningStationOwned);
            if (session.SharpeningStationOwned)
                ProductCard("Кухонный нож", "Один тупой нож · в отдельную ячейку инвентаря", "$40", knifeIcon,
                    () => Execute("knife"), !session.KnifePurchased && session.Balance >= 40, "Нужно $40", session.KnifePurchased);
        }
        private void SharpeningOffer()
        {
            if (!session.SharpeningStationOwned) return;
            var order = session.Order(DealOrderId.Sharpen);
            Line("Заточка: один кухонный нож · $500 · " + (order.State == DealOrderState.Accepted ?
                WorkshopView.Clock(Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds)) : "18:00 с принятия"));
            if (order.State == DealOrderState.Available) Button("Принять заточку", () => Execute("sharpaccept"));
            if (order.State == DealOrderState.Available || order.State == DealOrderState.Accepted)
                Button(order.State == DealOrderState.Accepted ? "Отменить заточку навсегда" : "Отказаться от заточки", () => Execute("sharpclose"));
            if (order.State == DealOrderState.Accepted)
                Button("Передать проверенный нож", () => Execute("sharpdeliver"), session.KnifePresented && session.KnifeReady);
            else Line("Заточка: " + State(order.State));
        }
        private void KnifeBuyback()
        {
            if (session.KnifeOwned)
                Button("Продать нож · $" + (session.KnifeReady ? 60 : 20) + " · закрывает заказ заточки",
                    () => Execute("knifesell"), session.KnifePresented);
        }
    }
}
