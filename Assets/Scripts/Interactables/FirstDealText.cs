using Inventory;

namespace Interactables
{
    // Small, scenario-specific presentation vocabulary; the domain keeps its existing messages.
    public static class FirstDealText
    {
        public static string Condition(FlashlightUnit unit) => !unit.IsTested ? "не проверен" : unit.LastTestPassed ? "исправен, проверен" : "неисправен: батарея";
        public static string State(DealOrderState state)
        {
            switch (state)
            {
                case DealOrderState.Locked: return "закрыт";
                case DealOrderState.Available: return "можно принять";
                case DealOrderState.Accepted: return "принят";
                case DealOrderState.Completed: return "выполнен";
                case DealOrderState.Expired: return "срок истёк";
                case DealOrderState.Failed: return "сорван";
                case DealOrderState.Declined: return "отклонён";
                default: return "отменён";
            }
        }

        public static string Message(string text)
        {
            if (text.StartsWith("Cannot: ")) return "Нельзя: " + Message(text.Substring(8));
            if (text.StartsWith("Purchased three lamps")) return "Партия куплена: −$180. Забери ящик у продавца и отнеси в мастерскую.";
            if (text.StartsWith("Accepted ")) return "Заказ принят. Проверь условия на доске и принеси исправные проверенные фонари до срока.";
            if (text.StartsWith("Delivered A")) return "Заказ A выполнен: +$260. Два фонаря переданы покупателю.";
            if (text.StartsWith("Delivered RepeatB")) return "Повторный заказ Б выполнен: +$110. Фонарь передан покупателю.";
            if (text.StartsWith("Delivered B")) return "Заказ Б выполнен: +$125. На доске появился один повторный заказ за $110.";
            if (text.StartsWith("Order ")) return "Заказ закрыт. Деньги и фонари не изменились; отмена принятого заказа отмечается как нарушение.";
            if (text.StartsWith("Battery purchased")) return "Батарея куплена: −$50. Расходуется только при ремонте на столе.";
            if (text.StartsWith("Present exactly")) return "Нужен полный комплект проверенных исправных фонарей. Для A принеси два сразу в ящике; для Б — один.";
            switch (text)
            {
                case "This offer is not available.": return "Это предложение пока недоступно или уже закрыто.";
                case "This single lot has already been purchased.": return "Эта единственная партия уже куплена.";
                case "Not enough money: the lot costs $180.": return "Недостаточно денег: партия стоит $180.";
                case "Not enough money: battery costs $50.": return "Недостаточно денег: батарея стоит $50.";
                case "Close the menu first.": return "Сначала закрой меню.";
                case "Dock the crate first.": return "Сначала поставь ящик на место в мастерской.";
                case "Return the lamp before carrying the crate.": return "Сначала верни фонарь в ящик.";
                case "Crate in hands. All stored lamps move with it.": return "Ящик в руках. Все лежащие внутри фонари перемещаются вместе с ним.";
                case "Crate returned to its dock.": return "Ящик поставлен в мастерской. Теперь доставай фонари кнопками 1 / 2 / 3.";
                case "Lamp returned to the crate.": return "Фонарь возвращён в ящик.";
                case "This lamp is not in your crate.": return "Этого фонаря нет в твоём ящике: он на столе или уже продан.";
                case "Hands occupied. Store or place that lamp first.": return "Руки заняты. Поставь фонарь на стол или верни в ящик.";
                case "Hands occupied; bench already contains a lamp.": return "Руки и стол заняты. Сначала верни фонарь в ящик.";
                case "Lamp retrieved from bench.": return "Фонарь в руках. Верни его в ящик той же кнопкой 1 / 2 / 3.";
                case "Take a lamp from the crate first.": return "Сначала возьми фонарь: E на кнопке 1 / 2 / 3 у ящика.";
                case "Lamp placed on bench. Run the test.": return "Фонарь на столе. Нажми E на кнопке «ПРОВЕРИТЬ».";
                case "Place a lamp on the bench first.": return "Сначала поставь фонарь на стол: возьми из ящика, наведи на стол и нажми E.";
                case "Tested: working": return "Проверен: исправен. Его примут покупатели.";
                case "Tested: battery fault": return "Проверен: неисправна батарея. Для ремонта нужна запасная за $50.";
                case "Test the lamp before replacing its battery.": return "Сначала проверь фонарь кнопкой «ПРОВЕРИТЬ».";
                case "This lamp does not need a replacement battery.": return "Этот фонарь уже исправен. Менять батарею не нужно.";
                case "Buy a spare battery for $50 first.": return "Купи у продавца запасную батарею за $50.";
                case "Battery replaced. +04:00 game time. Run a new test.": return "Батарея заменена. Время +04:00. Обязательно проверь фонарь снова.";
                case "Accept this order before delivering; closed orders cannot pay again.": return "Сначала прими заказ на доске. Выполненный или закрытый заказ повторно не оплачивается.";
                case "The deadline has passed.":
                case "The deadline has passed. No payment.": return "Срок истёк. Оплаты нет; фонари остаются у тебя.";
                case "Test every presented lamp on the workbench first.": return "Сначала проверь каждый фонарь на рабочем столе.";
                case "Only tested working lamps qualify. Repair and retest the faulty lamp.": return "Принимаются только исправные проверенные фонари. Неисправный нужно починить и проверить снова.";
                case "Bring your lamps here in your hands or carried crate; stored and sold lamps cannot transfer.": return "Принеси фонари сюда в руках или в переносимом ящике. Со склада и повторно проданное не передаётся.";
                case "Buy the seller's lot first: three lamps for $180.": return "Сначала купи у продавца партию: три фонаря за $180.";
                case "Bring the crate to this station first.": return "Сначала принеси ящик к этой точке.";
                default:
                    if (text.StartsWith("Lamp in hands:")) return "Фонарь в руках. Поставь его на рабочий стол через E.";
                    return text;
            }
        }
    }
}
