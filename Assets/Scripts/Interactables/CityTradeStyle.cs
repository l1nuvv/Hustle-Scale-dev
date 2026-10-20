using System;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private Sprite roundedUi, lampIcon, batteryIcon;
        private Texture2D roundedTexture, lampTexture, batteryTexture;
        private Text menuWallet;
        private static readonly Color Ink = new Color(0.055f, 0.07f, 0.085f);
        private static readonly Color Paper = new Color(0.94f, 0.95f, 0.93f);
        private static readonly Color Muted = new Color(0.58f, 0.64f, 0.66f);
        private static readonly Color Mint = new Color(0.64f, 0.86f, 0.73f);

        private void BuildUiArtwork()
        {
            roundedTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            roundedTexture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    var q = new Vector2(Mathf.Max(Mathf.Abs(x - 31.5f) - 15.5f, 0), Mathf.Max(Mathf.Abs(y - 31.5f) - 15.5f, 0));
                    roundedTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(16.5f - q.magnitude)));
                }
            roundedTexture.Apply(false, true);
            roundedUi = Sprite.Create(roundedTexture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100, 0,
                SpriteMeshType.FullRect, Vector4.one * 20);
            lampIcon = ProductArtwork(false, out lampTexture);
            batteryIcon = ProductArtwork(true, out batteryTexture);
        }

        // Small illustrations are native UI assets, built once and shared by every menu.
        private static Sprite ProductArtwork(bool battery, out Texture2D texture)
        {
            texture = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var cells = new[] { new Vector2(0.32f, 0.46f), new Vector2(0.63f, 0.54f) };
            for (var y = 0; y < 192; y++)
                for (var x = 0; x < 192; x++)
                {
                    var p = new Vector2((x + 0.5f) / 192, (y + 0.5f) / 192);
                    var pixel = Color.clear;
                    if (battery)
                    {
                        foreach (var cell in cells)
                        {
                            var q = p - cell;
                            if (Mathf.Abs(q.x) < 0.09f && q.y > 0.26f && q.y < 0.31f) pixel = new Color(0.7f, 0.75f, 0.73f);
                            if (Mathf.Abs(q.x) < 0.13f && Mathf.Abs(q.y) < 0.27f) pixel = Mint;
                            if (Mathf.Abs(q.x) < 0.13f && q.y > 0.16f && q.y < 0.23f) pixel = Ink;
                            if (Mathf.Abs(q.x) < 0.13f && q.y < -0.18f && q.y > -0.24f) pixel = Ink;
                            if (Mathf.Abs(q.x) < 0.035f && Mathf.Abs(q.y - 0.04f) < 0.008f ||
                                Mathf.Abs(q.x) < 0.008f && Mathf.Abs(q.y - 0.04f) < 0.035f) pixel = Ink;
                        }
                    }
                    else
                    {
                        var steel = new Color(0.63f, 0.71f, 0.72f);
                        if (Segment(p, new Vector2(0.33f, 0.39f), new Vector2(0.19f, 0.13f), 0.025f) ||
                            Segment(p, new Vector2(0.67f, 0.39f), new Vector2(0.81f, 0.13f), 0.025f) ||
                            Segment(p, new Vector2(0.19f, 0.13f), new Vector2(0.81f, 0.13f), 0.025f)) pixel = steel;
                        if (Mathf.Abs(p.x - 0.5f) < 0.2f && p.y > 0.72f && p.y < 0.85f) pixel = steel;
                        if (Mathf.Abs(p.x - 0.5f) < 0.14f && p.y > 0.74f && p.y < 0.81f) pixel = Color.clear;
                        if (Mathf.Abs(p.x - 0.5f) < 0.33f && p.y > 0.36f && p.y < 0.75f) pixel = Ink;
                        if (Mathf.Abs(p.x - 0.5f) < 0.28f && p.y > 0.41f && p.y < 0.7f) pixel = new Color(0.92f, 0.72f, 0.35f);
                        if (Mathf.Abs(p.x - 0.5f) < 0.23f && p.y > 0.45f && p.y < 0.66f) pixel = new Color(1, 0.9f, 0.62f);
                        if (p.x > 0.32f && p.x < 0.69f && p.y > 0.46f && p.y < 0.65f && (int)(p.x * 60) % 6 == 0) pixel = new Color(0.86f, 0.64f, 0.28f);
                    }
                    texture.SetPixel(x, y, pixel);
                }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 192, 192), Vector2.one * 0.5f);
        }

        private static bool Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            var edge = b - a;
            return (p - a - edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude)).sqrMagnitude < radius * radius;
        }

        private Image Round(Transform target, Color color)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.sprite = roundedUi; image.type = Image.Type.Sliced; image.color = color;
            return image;
        }

        private void ProductCard(string title, string detail, string price, Sprite icon, Action purchase, bool available, string unavailable = "Нет денег")
        {
            var card = Rect(title, menuContent, Vector2.zero, Vector2.one);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 124 * textScale;
            Round(card, new Color(0.12f, 0.15f, 0.175f));
            var art = Rect("Product illustration", card, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            art.pivot = new Vector2(0, 0.5f); art.anchoredPosition = new Vector2(16, 0); art.sizeDelta = new Vector2(96, 96);
            Round(art, new Color(0.085f, 0.11f, 0.13f));
            var picture = Rect("Icon", art, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            picture.sprite = icon; picture.raycastTarget = false;
            var titleLabel = Label(card, title, 22); titleLabel.fontStyle = FontStyle.Bold;
            titleLabel.alignment = TextAnchor.LowerLeft;
            titleLabel.rectTransform.anchorMin = new Vector2(0, 0.52f); titleLabel.rectTransform.anchorMax = new Vector2(1, 1);
            titleLabel.rectTransform.offsetMin = new Vector2(132, 0); titleLabel.rectTransform.offsetMax = new Vector2(-170, -14);
            var description = Label(card, detail, 15); description.color = Muted; description.alignment = TextAnchor.UpperLeft;
            description.rectTransform.anchorMin = Vector2.zero; description.rectTransform.anchorMax = new Vector2(1, 0.47f);
            description.rectTransform.offsetMin = new Vector2(132, 12); description.rectTransform.offsetMax = new Vector2(-170, 0);
            var priceLabel = Label(card, price, 26); priceLabel.fontStyle = FontStyle.Bold; priceLabel.alignment = TextAnchor.MiddleRight;
            priceLabel.rectTransform.anchorMin = new Vector2(1, 0.51f); priceLabel.rectTransform.anchorMax = Vector2.one;
            priceLabel.rectTransform.offsetMin = new Vector2(-154, 0); priceLabel.rectTransform.offsetMax = new Vector2(-22, -10);
            var buy = ActionButton(card, available ? "Купить" : unavailable, purchase, available);
            var rect = buy.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0); rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-20, 16 + 40 * textScale); rect.sizeDelta = new Vector2(134, 40 * textScale);
            buy.GetComponent<LayoutElement>().ignoreLayout = true;
            buy.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            SetButtonTone(buy, true);
        }

        private static void SetButtonTone(Button button, bool primary)
        {
            var colors = button.colors;
            colors.fadeDuration = 0;
            colors.normalColor = primary ? Mint : new Color(0.14f, 0.18f, 0.21f);
            colors.highlightedColor = colors.selectedColor = primary ? new Color(0.8f, 0.98f, 0.87f) : new Color(0.23f, 0.3f, 0.33f);
            colors.pressedColor = primary ? new Color(0.45f, 0.7f, 0.55f) : new Color(0.1f, 0.14f, 0.16f);
            colors.disabledColor = new Color(0.1f, 0.12f, 0.14f);
            button.colors = colors;
            button.GetComponentInChildren<Text>().color = !button.interactable ? Muted : primary ? Ink : Paper;
        }

        private void FitTradeMenu()
        {
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Mathf.Min(current == null ? 960 : 760, safeArea.rect.width - 48), rect.sizeDelta.y);
            Canvas.ForceUpdateCanvases();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x,
                Mathf.Min(safeArea.rect.height * 0.88f, LayoutUtility.GetPreferredHeight(menuContent) + 132));
            Canvas.ForceUpdateCanvases();
        }

        private Button BackButton()
        {
            var footer = Rect("Menu footer", menuContent, Vector2.zero, Vector2.one);
            footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 40 * textScale;
            var button = ActionButton(footer, "Назад", CloseMenu);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0, 1);
            rect.pivot = Vector2.zero; rect.sizeDelta = new Vector2(104 * textScale, 0);
            var colors = button.colors; colors.normalColor = Color.clear; button.colors = colors;
            var key = Label(footer, "Esc", 12); key.color = Muted; key.alignment = TextAnchor.MiddleLeft;
            key.rectTransform.anchorMax = new Vector2(0, 1);
            key.rectTransform.offsetMin = new Vector2(112 * textScale, 0);
            key.rectTransform.offsetMax = new Vector2(155 * textScale, 0);
            return button;
        }

        private void DisposeUiArtwork()
        {
            Destroy(roundedUi); Destroy(lampIcon); Destroy(batteryIcon);
            Destroy(roundedTexture); Destroy(lampTexture); Destroy(batteryTexture);
        }
    }
}
