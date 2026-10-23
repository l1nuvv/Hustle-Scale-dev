using System;
using System.Collections.Generic;
using Inventory;
using Player.Movement;
using UnityEngine;

namespace Interactables
{
    // The host supplies clock, spawn and save adapters; no scene lookup or scene name.
    public sealed class CheatMenu : MonoBehaviour
    {
        public static readonly IReadOnlyList<(string Label, string Command)> Entries = Array.AsReadOnly(new[]
        {
            ("Деньги: +$1 000", "money1000"), ("Деньги: +$10 000", "money10000"),
            ("Деньги: установить $0", "money0"), ("Выдать 10 батарей бесплатно", "batteries"),
            ("Выдать 10 доз припоя бесплатно", "solder"), ("Проверить все свои обычные фонари", "diagnose"),
            ("Ходьба / спринт: ×1", "speed1"), ("Ходьба / спринт: ×2", "speed2"), ("Ходьба / спринт: ×5", "speed5"),
            ("Заморозить активное время заказов", "clock0"), ("Активное время заказов: ×1", "clock1"),
            ("Активное время заказов: ×5", "clock5"), ("Время +1 минута · может провалить заказ", "advance60"),
            ("Время +10 минут · может провалить заказ", "advance600"), ("Телепорт к старту", "spawn"),
            ("Показать состояние и ID предметов", "status"), ("Вернуть обычные скорости и время", "reset")
        });
        private WorkshopSession session;
        private PlayerController player;
        private Func<float> getClock;
        private Action<float> setClock;
        private Transform spawn;
        private Action requestSave;
        public event Action Changed;
        public string Message { get; private set; } = "";
        public string Status => session == null ? "Нет игровой сессии." :
            "$" + session.Balance + " · Ходьба ×" + player.CheatSpeedMultiplier + " · Время ×" + getClock() + "\n" +
            (Message.Length > 0 ? Message : "Деньги и предметы попадут в автосохранение.\nСкорости сбрасываются при загрузке / новой игре.");

        public void Bind(WorkshopSession target, PlayerController movement, Func<float> clock,
            Action<float> changeClock, Transform spawnPoint, Action save)
        {
            session = target; player = movement; getClock = clock; setClock = changeClock;
            spawn = spawnPoint; requestSave = save; Message = "";
        }

        public bool Available(string command) => session != null && session.IsCity && session.IsPaused &&
            (command != "solder" || session.StationOwned);

        public bool Execute(string command)
        {
            if (!Available(command)) return false;
            var success = true;
            string result;
            switch (command)
            {
                case "money1000":
                case "money10000":
                    var amount = command == "money1000" ? 1000 : 10000;
                    success = session.CheatSetBalance((int)Math.Min(1000000L, (long)session.Balance + amount), out result); break;
                case "money0": success = session.CheatSetBalance(0, out result); break;
                case "batteries": success = session.CheatSupplies(false, 10, out result); break;
                case "solder": success = session.CheatSupplies(true, 10, out result); break;
                case "diagnose": success = session.CheatDiagnose(out result); break;
                case "advance60":
                case "advance600":
                    success = session.CheatAdvanceTime(command == "advance60" ? 60 : 600, out result); break;
                case "speed1":
                case "speed2":
                case "speed5":
                    player.SetCheatSpeed(command == "speed1" ? 1 : command == "speed2" ? 2 : 5);
                    result = "Скорость ходьбы и спринта ×" + player.CheatSpeedMultiplier; break;
                case "clock0":
                case "clock1":
                case "clock5":
                    setClock(command == "clock0" ? 0 : command == "clock1" ? 1 : 5);
                    result = "Темп активного времени ×" + getClock() + ". Ремонт по-прежнему добавляет 4 минуты."; break;
                case "spawn":
                    player.RestorePose(spawn.position, spawn.eulerAngles.y, 0);
                    requestSave(); result = "Игрок перенесён к старту."; break;
                case "status":
                    var report = new System.Text.StringBuilder();
                    report.AppendLine("Время " + session.ElapsedSeconds.ToString("F1") + " с · батареи " + session.SpareBatteries + " · припой " + session.SolderDoses);
                    foreach (var unit in session.Units)
                        report.AppendLine(unit.InstanceId + " · " + unit.Location + " · " + unit.Condition);
                    if (session.SolderingLamp != null) report.AppendLine("Пайка: " + session.SolderingLamp.InstanceId);
                    if (session.KnifePurchased) report.AppendLine("Нож: " + session.KnifeId + " · " + session.KnifePlace);
                    Debug.Log("Cheats: " + report);
                    result = "Состояние и ID записаны в Unity Console / Player.log."; break;
                case "reset":
                    player.SetCheatSpeed(1); setClock(1);
                    result = "Обычные скорости восстановлены. Выданные деньги и предметы остаются."; break;
                default: return false;
            }
            if (success) session.MarkCheatsUsed();
            Message = result; Changed?.Invoke();
            return success;
        }
    }
}
