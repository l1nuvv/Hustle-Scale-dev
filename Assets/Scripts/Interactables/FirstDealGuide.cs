using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Contextual help reads the session; it never advances or chooses an economic route.
    public sealed class FirstDealGuide : MonoBehaviour
    {
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private FirstDealView deal;
        [SerializeField] private Text label;
        [SerializeField] private Text direction;
        [SerializeField] private Transform board;
        [SerializeField] private Transform seller;
        [SerializeField] private Transform dock;
        [SerializeField] private Transform bench;
        [SerializeField] private Transform crate;
        [SerializeField] private Transform buyerA;
        [SerializeField] private Transform buyerB;
        private Transform target;
        private Transform eye;
        private float nextDirectionUpdate;

        private void Start()
        {
            eye = Camera.main.transform;
            workshop.Session.Changed += Refresh;
            ApplyColors();
            Refresh();
        }

        private void OnDestroy()
        {
            if (workshop != null && workshop.Session != null) workshop.Session.Changed -= Refresh;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextDirectionUpdate) return;
            nextDirectionUpdate = Time.unscaledTime + 0.15f;
            if (target == null) { direction.text = ""; return; }
            var offset = eye.InverseTransformDirection(target.position - eye.position);
            var side = offset.z < 0 ? "позади — развернись" : Mathf.Abs(offset.x) < 1 ? "впереди" : offset.x < 0 ? "слева" : "справа";
            direction.text = "Нужная точка: " + side + " · " + Vector3.Distance(eye.position, target.position).ToString("0") + " м";
        }

        private void Show(string title, string help, Transform destination)
        {
            label.text = title + "\n" + help;
            target = destination;
        }

        private void Refresh()
        {
            var s = workshop.Session;
            if (s.ShiftEnded)
            { Show("СМЕНА ЗАВЕРШЕНА", "Итоги зафиксированы. Новая попытка доступна на итоговом экране.", null); return; }
            if (s.HasPrimarySale || s.BuybackAvailable ||
                (s.LotPurchased && !s.CanPursueOrder(DealOrderId.A) && !s.CanPursueOrder(DealOrderId.B)))
            {
                var nextRepeat = s.Order(DealOrderId.RepeatB).State;
                var destination = s.OwnedLampCount == 0 ? null : nextRepeat == DealOrderState.Available ? board :
                    nextRepeat == DealOrderState.Accepted ? crate : seller;
                Show("ВЫБЕРИ СЛЕДУЮЩИЙ ШАГ", s.OwnedLampCount == 0 ? "Товар продан. Esc → «Итоги»: завершить смену." :
                    "Осталось: " + s.OwnedLampCount + ". Условия продолжения — на доске; выкуп — у продавца. Esc — итоги.", destination);
                return;
            }
            if (!deal.OffersRead)
            { Show("ПРОЧИТАЙ ЗАКАЗЫ", "Синяя доска: наведи прицел и нажми E. В меню время остановлено.", board); return; }
            if (!s.LotPurchased)
            { Show("НАЙДИ ПРОДАВЦА", "Снаружи, у синей стойки: E — партия из трёх фонарей за $180.", seller); return; }
            if (!s.HasTransportedCrate)
            { Show("ЗАБЕРИ ЯЩИК", "Ящик рядом с продавцом: E — взять все фонари вместе.", crate); return; }
            if (s.OnBench != null)
            {
                Show(s.OnBench.IsTested ? "ЗАБЕРИ ПРОВЕРЕННЫЙ ФОНАРЬ" : "ПРОВЕРЬ ФОНАРЬ",
                    !s.OnBench.IsTested ? "E на синей кнопке «ПРОВЕРИТЬ»." : s.OnBench.LastTestPassed ?
                    "Исправен. E на столе — забрать; E на его кнопке у ящика — вернуть." :
                    "Батарея неисправна. Ремонт необязателен: $50 и +04:00, затем новый тест.", bench);
                return;
            }
            if (s.InHands != null)
            {
                Show(s.InHands.IsTested ? "ФОНАРЬ В РУКАХ" : "ПОСТАВЬ ФОНАРЬ НА СТОЛ",
                s.InHands.IsTested ? "F — свет · Q — поставить · G — убрать. Для продажи возьми в руки или верни в ящик." :
                "E на столе — поставить для диагностики. F — свет · Q — поставить в другом месте · G — убрать.", s.InHands.IsTested ? null : bench); return;
            }
            foreach (var unit in s.Units)
                if (unit.Location == FlashlightLocation.Pocket || unit.Location == FlashlightLocation.World)
                {
                    Show("ФОНАРИ МОЖНО ПЕРЕНОСИТЬ СВОБОДНО", "Tab — инвентарь · 1–3 — взять фонарь · E — подобрать с поверхности. Диагностика и ремонт — на столе.", bench);
                    return;
                }
            var working = 0;
            var unknown = 0;
            foreach (var unit in s.Units)
            {
                if (unit.Location != FlashlightLocation.Crate) continue;
                if (!unit.IsTested) unknown++;
                else if (unit.LastTestPassed) working++;
            }
            var a = s.Order(DealOrderId.A);
            var b = s.Order(DealOrderId.B);
            var repeat = s.Order(DealOrderId.RepeatB);
            if (s.IsCrateCarried)
            {
                if (a.State == DealOrderState.Accepted && working >= 2)
                    Show("ДОСТАВЬ ЗАКАЗ A", "Оранжевая стойка: E — передать 2 исправных проверенных до 09:00.", buyerA);
                else if ((b.State == DealOrderState.Accepted || repeat.State == DealOrderState.Accepted) && working >= 1)
                    Show("ДОСТАВЬ ЗАКАЗ Б", "Зелёная стойка: E — передать 1 исправный проверенный до 18:00.", buyerB);
                else Show("ПРИНЕСИ ЯЩИК В МАСТЕРСКУЮ", "E на оранжевой кнопке «ПОСТАВИТЬ ЯЩИК».", dock);
                return;
            }
            if (working > 0 && (a.State == DealOrderState.Accepted || b.State == DealOrderState.Accepted || repeat.State == DealOrderState.Accepted))
            { Show("ПОДГОТОВЬ ДОСТАВКУ", "Исправных проверенных: " + working + ". Для A нужны 2, для Б — 1. E на ящике — взять.", crate); return; }
            if (working > 0 && (s.CanPursueOrder(DealOrderId.A) || s.CanPursueOrder(DealOrderId.B)))
            { Show("ВЫБЕРИ СДЕЛКУ", "Исправных проверенных: " + working + ". E на доске — условия и принятие заказа.", board); return; }
            if (unknown > 0)
            { Show("ДОСТАНЬ ФОНАРЬ", "E на одной из кнопок 1 / 2 / 3 у ящика.", crate); return; }
            Show("ПРОВЕРЬ ВОЗМОЖНОСТИ", "E на доске — оставшиеся заказы. Esc → «Итоги» — завершение смены.", board);
        }

        private static void ApplyColors()
        {
            foreach (var r in FindObjectsByType<Renderer>())
            {
                Color color;
                if (r.name.StartsWith("Readable.Blue")) color = new Color(0.18f, 0.31f, 0.37f);
                else if (r.name.StartsWith("Readable.Green")) color = new Color(0.23f, 0.34f, 0.25f);
                else if (r.name.StartsWith("Readable.Orange")) color = new Color(0.55f, 0.31f, 0.17f);
                else if (r.name.StartsWith("Readable.Wood")) color = new Color(0.4f, 0.23f, 0.1f);
                else if (r.name.StartsWith("Readable.White")) color = new Color(1, 0.96f, 0.74f);
                else if (r.name.StartsWith("Readable.Dark") || r.name == "Crate visual") color = new Color(0.08f, 0.1f, 0.12f);
                else if (r.name.StartsWith("Crate selector")) color = new Color(1, 0.38f, 0.05f);
                else if (r.name.StartsWith("Lamp ") && r.GetComponent<TextMesh>() == null) color = new Color(1, 0.72f, 0.05f);
                else continue;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
