using System.Collections;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;

namespace Player
{
    public sealed partial class CityPlayerVerification
    {
        // Separate fixture build, never a route from the production city. Scripted
        // component/model calls verify integration, not human navigation or OS input.
        private IEnumerator FirstDealScenario()
        {
            for (var run = 0; run < 3; run++)
            {
                var workshop = FindAnyObjectByType<WorkshopView>();
                var deal = FindAnyObjectByType<FirstDealView>();
                var controller = FindAnyObjectByType<PlayerController>();
                Check(workshop != null && deal != null && controller != null, "FIRST DEAL fixture components");
                var session = workshop.Session;
                Check(deal.MenuOpen && session.IsPaused && session.Balance == 300 && !session.ShiftStarted,
                    "fresh introduction after scene start/restart " + run);
                Check(FindObjectsByType<WorkshopView>().Length == 1 && FindObjectsByType<FirstDealView>().Length == 1,
                    "no duplicated fixture composition");
                deal.SendMessage("OnApplicationFocus", true); // Synthetic fixture callback, not an Alt+Tab test.
                deal.CloseMenu();
                controller.RestorePose(new Vector3(0, 0.08f, -2), 0, 0);
                deal.Perform(FirstDealActionKind.Offers);
                Check(session.ShiftStarted && deal.OffersRead, "leaving workshop starts shift and offers can be read");
                if (run < 2) { deal.Accept(0); deal.Accept(1); }
                deal.CloseMenu(); deal.Perform(FirstDealActionKind.Seller); deal.Purchase(false);
                Check(session.LotPurchased && session.Balance == 120 && !deal.MenuOpen, "purchase through seller adapter");
                for (var i = 0; i < 3; i++)
                {
                    Check(session.ToggleUnit(i, out _), "take fixture lamp " + i);
                    workshop.Perform(WorkshopActionKind.Bench, i);
                    Check(session.OnBench == session.Units[i], "bench adapter " + i);
                    workshop.Perform(WorkshopActionKind.Test, i);
                    Check(session.Units[i].IsTested, "diagnosis adapter " + i);
                    if (i == 1 && run < 2)
                    {
                        Check(!session.Units[i].LastTestPassed, "fixed FIRST DEAL fault");
                        deal.OpenMenu(true); deal.Purchase(true);
                        workshop.Perform(WorkshopActionKind.Repair, i);
                        Check(session.SpareBatteries == 0 && session.RepairSeconds == 240 && !session.Units[i].IsTested,
                            "repair consumes battery and invalidates diagnosis");
                        workshop.Perform(WorkshopActionKind.Test, i);
                        Check(session.Units[i].LastTestPassed, "retest after repair");
                    }
                    workshop.Perform(WorkshopActionKind.Bench, i);
                    Check(session.ToggleUnit(i, out _), "return fixture lamp " + i);
                }
                Check(session.ToggleCrate(out _), "present carried crate");
                if (run < 2)
                {
                    var first = run == 0 ? FirstDealActionKind.DeliverA : FirstDealActionKind.DeliverB;
                    var second = run == 0 ? FirstDealActionKind.DeliverB : FirstDealActionKind.DeliverA;
                    deal.Perform(first); deal.Perform(second);
                    Check(session.Balance == 455 && session.OwnedLampCount == 0 &&
                        session.Order(DealOrderId.A).State == DealOrderState.Completed &&
                        session.Order(DealOrderId.B).State == DealOrderState.Completed, "both primary orders settle once");
                    deal.Perform(first);
                    Check(session.Balance == 455, "duplicate delivery has no payout");
                }
                else
                {
                    session.AdvanceActiveSeconds(1080);
                    Check(session.Order(DealOrderId.A).State == DealOrderState.Expired &&
                        session.Order(DealOrderId.B).State == DealOrderState.Expired, "both deadlines expire");
                    deal.OpenMenu(true); deal.SellBack();
                    Check(session.Balance == 275 && session.OwnedLampCount == 0, "reserve route settles $155");
                }
                deal.OpenSummary();
                var elapsed = session.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(0.1f);
                Check(session.ElapsedSeconds == elapsed && session.IsPaused && !controller.CanInteract,
                    "summary pauses clock and input");
                deal.FinishShift();
                if (run == 2) { Check(!session.ShiftEnded, "abandonment requires confirmation"); deal.FinishShift(); }
                Check(session.ShiftEnded && deal.MenuOpen, "shift finished through summary");
                deal.CloseMenu();
                Check(deal.MenuOpen && session.IsPaused, "finished shift cannot resume");
                events.Add("FIRST DEAL route " + run + ": balance " + session.Balance + "; finish; restart scene");
                deal.RestartShift();
                yield return null; yield return null; yield return null;
                var fresh = FindAnyObjectByType<WorkshopView>();
                Check(fresh.Session != session && fresh.Session.Balance == 300 && !fresh.Session.LotPurchased,
                    "real scene reload creates fresh fixture state");
            }
        }
    }
}
