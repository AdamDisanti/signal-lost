using System.Text;
using SignalLost.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>Shared pieces for the pockets menu and the shop: item rows, prices and slot names.</summary>
    public static class ItemRows
    {
        public static string SlotName(UpgradeSlot slot) => slot switch
        {
            UpgradeSlot.Laser => "Mining Laser",
            UpgradeSlot.Scanner => "Scanner",
            UpgradeSlot.Flask => "Scotch Flask",
            UpgradeSlot.Suit => "Suit",
            UpgradeSlot.Supply => "Supply",
            _ => "Trade"
        };

        public static Color SlotColor(UpgradeSlot slot) => slot switch
        {
            UpgradeSlot.Laser => UiTheme.Accent,
            UpgradeSlot.Scanner => UiTheme.SpeakerColor("Cavendish"),
            UpgradeSlot.Flask => new Color(1f, 0.78f, 0.34f),
            UpgradeSlot.Suit => new Color(0.93f, 0.48f, 0.15f),
            _ => UiTheme.Muted
        };

        /// <summary>The price, each part coloured by type, or greyed out if the player is short of it.</summary>
        public static string Price(Upgrade upgrade, PlayerInventory inventory)
        {
            var price = new StringBuilder();
            foreach ((ScrapType type, int amount) in upgrade.Cost)
            {
                bool enough = inventory != null && inventory.Scrap(type) >= amount;
                price.Append($"<color={UiTheme.Hex(enough ? ScrapInfo.Color(type) : UiTheme.Muted)}>{amount} {ScrapInfo.Name(type)}</color>   ");
            }
            if (upgrade.KeyItem != null)
            {
                bool has = inventory != null && inventory.HasKeyItem(upgrade.KeyItem);
                price.Append($"<color={UiTheme.Hex(has ? UiTheme.Good : UiTheme.Warning)}>+ {upgrade.KeyItem}</color>");
            }
            return price.ToString().TrimEnd();
        }

        /// <summary>A panel row with an icon, a title and a line of text; returns the row.</summary>
        public static RectTransform Row(Transform parent, string name, Sprite icon, string title, Color titleColor, string body,
            Vector2 position, Vector2 size, float rightReserve = 250f)
        {
            Image row = UiTheme.Panel(name, parent, UiTheme.PanelLight, null, 0.7f);
            RectTransform rect = UiTheme.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            float left = 20f;
            if (icon != null)
            {
                Image image = UiTheme.Icon("Icon", rect, icon);
                float side = Mathf.Min(size.y - 16f, 84f);
                UiTheme.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(side, side));
                left = side + 26f;
            }
            TextMeshProUGUI heading = UiTheme.Label("Title", rect, 28f, titleColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -10f), new Vector2(size.x - left - 20f, 36f));
            heading.text = title;
            TextMeshProUGUI text = UiTheme.Label("Body", rect, 21f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            UiTheme.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -44f), new Vector2(Mathf.Max(80f, size.x - left - rightReserve), size.y - 50f));
            text.text = body;
            return rect;
        }

        /// <summary>A label on the right of a row (a count or a status).</summary>
        public static TextMeshProUGUI RightLabel(RectTransform row, string text, float size, Color color, float width = 240f)
        {
            TextMeshProUGUI label = UiTheme.Label("Right", row, size, color, TextAlignmentOptions.Right, FontStyles.Bold);
            UiTheme.Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(width, 60f));
            label.text = text;
            return label;
        }
    }

    /// <summary>Lets the player spin the pockets menu's mechanic by dragging it.</summary>
    public sealed class DragToTurn : MonoBehaviour, IDragHandler
    {
        public GearFigure Figure;
        public void OnDrag(PointerEventData data)
        {
            if (Figure != null) Figure.Turn(-data.delta.x * 0.5f);
        }
    }
}
