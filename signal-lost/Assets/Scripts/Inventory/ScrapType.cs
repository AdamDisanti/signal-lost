using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>Placeholder scrap materials (meeting notes 2026-10-20). Append only: drop tables store the number.</summary>
    public enum ScrapType { ScrapMetal, Wiring, CircuitBoards, FuelCells }

    /// <summary>Display names, colours and flavour text for each scrap type.</summary>
    public static class ScrapInfo
    {
        public static readonly ScrapType[] All = { ScrapType.ScrapMetal, ScrapType.Wiring, ScrapType.CircuitBoards, ScrapType.FuelCells };

        public static string Name(ScrapType type)
        {
            switch (type)
            {
                case ScrapType.Wiring: return "Wiring";
                case ScrapType.CircuitBoards: return "Circuit Boards";
                case ScrapType.FuelCells: return "Fuel Cells";
                default: return "Scrap Metal";
            }
        }

        public static Color Color(ScrapType type)
        {
            switch (type)
            {
                case ScrapType.Wiring: return new Color(0.95f, 0.55f, 0.25f);
                case ScrapType.CircuitBoards: return new Color(0.36f, 0.85f, 0.45f);
                case ScrapType.FuelCells: return new Color(1f, 0.85f, 0.25f);
                default: return new Color(0.72f, 0.76f, 0.82f);
            }
        }

        /// <summary>The type's icon inside text (any UiTheme label shows it): "4 " + Inline(type) reads "4 [icon]".</summary>
        public static string Inline(ScrapType type) => $"<sprite name=\"{type}\">";

        /// <summary>An amount with its icon, e.g. "4 [icon]".</summary>
        public static string Amount(ScrapType type, int amount) => amount + " " + Inline(type);

        public static string Blurb(ScrapType type)
        {
            switch (type)
            {
                case ScrapType.Wiring: return "Copper spaghetti. Critters love chewing it.";
                case ScrapType.CircuitBoards: return "Smart bits. Some still beep sadly.";
                case ScrapType.FuelCells: return "Handle with care. Do not lick.";
                default: return "Bent hull plating. The basics of everything.";
            }
        }
    }
}
