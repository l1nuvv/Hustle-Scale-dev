namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public bool TakeCrateContents(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!IsCity || IsCrateCarried || IsCrateInVehicle)
                return Reject("Сначала поставь ящик.", out message);
            var count = 0;
            foreach (var unit in units) if (unit.Location == FlashlightLocation.Crate) count++;
            if (count == 0) return Reject("Ящик пуст.", out message);
            foreach (var unit in units)
                if (unit.Location == FlashlightLocation.Crate)
                {
                    unit.IsPowered = false;
                    unit.Location = FlashlightLocation.Pocket;
                }
            return Complete("Фонари в инвентаре: " + count + ". 1–3 — взять в руки.", out message);
        }

        public bool LoadVehicleCrate(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!IsCity || IsCrateInVehicle || !IsCrateCarried || InHands != null)
                return Reject("Возьми ящик в руки; с фонарём в руках погрузка невозможна.", out message);
            IsCrateCarried = false;
            IsCrateInVehicle = true;
            return Complete("Ящик в багажнике. E у заднего борта — взять.", out message);
        }

        // The city view validates physical access to the stopped vehicle.
        public bool UnloadVehicleCrate(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!IsCity || !IsCrateInVehicle || IsCrateCarried || InHands != null)
                return Reject("Для выгрузки ящика освободи руки.", out message);
            IsCrateInVehicle = false;
            IsCrateCarried = true;
            return Complete("Ящик в руках.", out message);
        }

        // Each of the three unique lamps has a persistent inventory slot; batteries share slot 4.
        // Storage never changes the physical delivery rules: goods must be presented in hands/crate.
        public bool StowHeld(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            var held = InHands;
            if (held == null) return Reject("В руках нет фонаря.", out message);
            held.IsPowered = false;
            held.Location = FlashlightLocation.Pocket;
            return Complete("Фонарь убран в инвентарь. 1–3 — взять в руки.", out message);
        }

        public bool EquipSlot(int index, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (index < 0 || index >= units.Length) return Reject("В этом слоте нет фонаря.", out message);
            var unit = units[index];
            if (unit.Location != FlashlightLocation.Pocket && unit.Location != FlashlightLocation.Hands)
                return Reject("Слот пуст: сначала забери фонарь из ящика, со стола или с поверхности.", out message);
            if (IsCrateCarried) return Reject("Сначала поставь ящик.", out message);
            if (unit.Location == FlashlightLocation.Hands) return StowHeld(out message);
            var held = InHands;
            if (held != null)
            {
                held.IsPowered = false;
                held.Location = FlashlightLocation.Pocket;
            }
            unit.Location = FlashlightLocation.Hands;
            return Complete("Фонарь " + (index + 1) + " в руках. F — свет, Q — поставить, G — убрать.", out message);
        }

        public bool PlaceHeld(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            var held = InHands;
            if (held == null) return Reject("Сначала возьми фонарь в руки.", out message);
            held.Location = FlashlightLocation.World;
            return Complete("Фонарь поставлен. E — подобрать.", out message);
        }

        public bool PickUpPlaced(int index, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (index < 0 || index >= units.Length || units[index].Location != FlashlightLocation.World)
                return Reject("Здесь нет твоего свободно стоящего фонаря.", out message);
            if (IsCrateCarried) return Reject("Сначала поставь ящик.", out message);
            var held = InHands;
            if (held != null)
            {
                held.IsPowered = false;
                held.Location = FlashlightLocation.Pocket;
            }
            units[index].Location = FlashlightLocation.Hands;
            return Complete("Фонарь подобран. F — свет, Q — поставить, G — убрать.", out message);
        }

        public bool TogglePower(int index, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (index < 0 || index >= units.Length || !IsOwned(units[index]))
                return Reject("Это не твой фонарь.", out message);
            var unit = units[index];
            if (unit.Location == FlashlightLocation.Crate || unit.Location == FlashlightLocation.Pocket)
                return Reject("Сначала достань фонарь.", out message);
            if (unit.Condition != FlashlightCondition.Working)
                return Reject("Фонарь не включается. Проверь батарею на столе.", out message);
            unit.IsPowered = !unit.IsPowered;
            // Switching power never marks goods as professionally tested for an order.
            return Complete(unit.IsPowered ? "Фонарь включён. F — выключить." : "Фонарь выключен. F — включить.", out message);
        }
    }
}
