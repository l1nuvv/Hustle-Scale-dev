using Inventory;
using Player;
using Player.Movement;
using UnityEngine;

namespace Interactables
{
    public sealed partial class SharpeningTrial
    {
        private CitySaveController citySave;
        private CityGreyboxWalk cityWalk;
        private CityTradeView cityTrade;
        private bool retrieveAfterExit;
        public bool IsCityOperation => citySave != null;
        private string CityPrompt => retrieveAfterExit && citySave.Session?.KnifePlace == KnifeLocation.Station ?
            "E — забрать нож · E с ножом — продолжить" : "E — заточка · принеси нож в руках";
        public void ConfigureCity(CitySaveController save, CityGreyboxWalk walk, CityTradeView trade, PlayerController owner, Camera camera)
        {
            citySave = save; cityWalk = walk; cityTrade = trade; player = owner; view = camera;
            citySave.SessionReplaced += BindCityKnife;
            citySave.BeforeExplicitSave += NormalizeCityKnife;
        }
        private void BindCityKnife()
        {
            if (edgeProperties == null) return;
            Exit();
            state = citySave.Session.SharpeningState;
            retrieveAfterExit = false;
            SetCityPattern();
            RefreshKnife();
            RefreshCityKnife();
        }
        private void SetCityPattern()
        {
            var random = new System.Random(citySave.Session.KnifeCoatingSeed);
            coatingPattern = new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                .85f + (float)random.NextDouble() * .4f, .8f + (float)random.NextDouble() * .4f);
        }
        public void RefreshCityKnife()
        {
            if (!IsCityOperation || knife == null) return;
            knife.gameObject.SetActive(citySave.Session.KnifeOwned && citySave.Session.KnifePlace == KnifeLocation.Station);
        }
        private bool EnterCity()
        {
            var session = citySave.Session;
            if (session == null || session.IsPaused || citySave.IsDriving) return false;
            if (retrieveAfterExit && session.KnifePlace == KnifeLocation.Station)
            {
                session.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out result);
                RefreshCityKnife(); return false;
            }
            if (!session.BeginSharpening(out result)) { cityTrade.ShowSharpeningMessage(result); return false; }
            state = session.SharpeningState;
            retrieveAfterExit = false;
            origin = new Vector3(0, 1.27f, -.14f); angle = 35;
            SetCityPattern(); RefreshCityKnife();
            result = "";
            return true;
        }
        private void NormalizeCityKnife()
        {
            if (!active) return;
            state.StopMotor(); gripping = false; consumed = true;
            StopEffects(); RefreshKnife();
        }
        private void LeaveCity()
        {
            citySave.Session.EndSharpening();
            retrieveAfterExit = true;
            cityTrade.enabled = true;
            RefreshCityKnife();
            cityTrade.RefreshSolderingWorld();
            citySave.RequestSave();
        }
    }
}
