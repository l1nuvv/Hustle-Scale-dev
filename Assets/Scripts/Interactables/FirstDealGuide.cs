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
            if (s.HasPrimarySale || s.BuybackAvailable)
            {
                var nextRepeat = s.Order(DealOrderId.RepeatB).State;
                var destination = s.OwnedLampCount == 0 ? null : nextRepeat == DealOrderState.Available ? board :
                    nextRepeat == DealOrderState.Accepted ? buyerB : seller;
                Show("ПОСЛЕДСТВИЯ И СЛЕДУЮЩИЙ ШАГ", "Деньги: $" + s.Balance + "; осталось фонарей: " + s.OwnedLampCount + ". " +
                    FirstDealText.NextOpportunity(s) + " Esc → «Итоги»: продолжить или завершить.", destination);
                return;
            }
            if (!deal.OffersRead)
            { Show("1. РАЗБЕРИСЬ В ПРЕДЛОЖЕНИЯХ", "Подойди к синей доске «ЗАКАЗЫ». Наведи на неё прицел и нажми E. Чтение останавливает время.", board); return; }
            if (!s.LotPurchased)
            { Show("2. НАЙДИ ПРОДАВЦА", "Выйди через проход. У синей стойки продавца нажми E и подтверди покупку трёх фонарей за $180. Можно уйти без покупки.", seller); return; }
            if (!s.HasTransportedCrate)
            { Show("3. ЗАБЕРИ СВОЙ ЯЩИК", "Фонари куплены. Наведи прицел на ящик рядом с продавцом и нажми E — все три поедут вместе.", crate); return; }
            if (s.OnBench != null)
            {
                Show(s.OnBench.IsTested ? "ЗАБЕРИ ПРОВЕРЕННЫЙ ФОНАРЬ" : "4. ПРОВЕРЬ ФОНАРЬ",
                    s.OnBench.IsTested ? "Наведи на стол и нажми E, затем на кнопку этого фонаря у ящика и снова E. Неисправный можно позже починить: батарея $50, время +04:00, затем новый тест." :
                    "Нажми E на синей кнопке «ПРОВЕРИТЬ». Исправный светит постоянно, неисправный мигает и гаснет.", bench);
                return;
            }
            if (s.InHands != null)
            { Show("ПОСТАВЬ ФОНАРЬ НА СТОЛ", "Подойди к деревянному рабочему столу. Наведи прицел на его столешницу и нажми E.", bench); return; }
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
                    Show("ДОСТАВЬ ЗАКАЗ A", "Оранжевая стойка A: наведи прицел и нажми E. Передаются сразу два проверенных исправных фонаря. Можно выбрать другую сделку.", buyerA);
                else if ((b.State == DealOrderState.Accepted || repeat.State == DealOrderState.Accepted) && working >= 1)
                    Show("ДОСТАВЬ ЗАКАЗ Б", "Зелёная стойка Б: E отдаёт один проверенный исправный фонарь. Для повторного заказа используй соседнюю кнопку «ПОВТОР Б».", buyerB);
                else Show("ДОНЕСИ ЯЩИК ДО МАСТЕРСКОЙ", "У мастерской наведи прицел на оранжевую кнопку «ПОСТАВИТЬ ЯЩИК» и нажми E. Затем можно доставать фонари по одному.", dock);
                return;
            }
            if (working > 0 && (a.State == DealOrderState.Accepted || b.State == DealOrderState.Accepted || repeat.State == DealOrderState.Accepted))
            { Show("ПОДГОТОВЬ ДОСТАВКУ", "Есть проверенные исправные: " + working + ". Возьми ящик кнопкой E. Для A нужны два сразу; для Б — один. Можно проверить оставшиеся фонари или вложиться в ремонт.", crate); return; }
            if (working > 0)
            { Show("5. ВЫБЕРИ СДЕЛКУ", "Есть проверенные исправные: " + working + ". На доске прими A или Б. A: два за $260 до 09:00. Б: один за $125 до 18:00. Затем забери ящик и отнеси покупателю.", board); return; }
            if (unknown > 0)
            { Show("4. ДОСТАНЬ ОДИН ФОНАРЬ", "У ящика три маленькие кнопки 1 / 2 / 3. Наведи на одну и нажми E. Затем поставь фонарь на стол через E и запусти «ПРОВЕРИТЬ».", crate); return; }
            Show("ПРОВЕРЬ ОСТАВШИЕСЯ ВОЗМОЖНОСТИ", FirstDealText.NextOpportunity(s) + " Esc → «Итоги»: продолжить или завершить.", board);
        }

        private static void ApplyColors()
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Color color;
                if (r.name.StartsWith("Readable.Blue")) color = new Color(0.08f, 0.4f, 0.85f);
                else if (r.name.StartsWith("Readable.Green")) color = new Color(0.12f, 0.62f, 0.3f);
                else if (r.name.StartsWith("Readable.Orange")) color = new Color(1, 0.38f, 0.05f);
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
