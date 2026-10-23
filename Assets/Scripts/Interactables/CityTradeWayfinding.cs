using System;
using System.Text.RegularExpressions;
using Inventory;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace Interactables
{
    // Presentation for this fixed neighbourhood; no change to trade rules or save data.
    public sealed partial class CityTradeView
    {

        private static string Location(FlashlightLocation location) => location switch
        {
            FlashlightLocation.Crate => "в ящике",
            FlashlightLocation.Hands => "в руках",
            FlashlightLocation.Bench => "на столе",
            FlashlightLocation.Seller => "у продавца",
            FlashlightLocation.BuyerA => "продан покупателю А",
            FlashlightLocation.BuyerB => "продан покупателю Б",
            FlashlightLocation.SolderBuyer => "передан по заказу пайки",
            FlashlightLocation.BoughtBack => "продан в скупку",
            FlashlightLocation.Pocket => "в кармане",
            _ => "в мире"
        };

        private static string CityMessage(string text)
        {
            if (text.StartsWith("Purchased three lamps", StringComparison.Ordinal)) return "Партия куплена · −$180";
            if (text.StartsWith("Battery purchased", StringComparison.Ordinal)) return "Батарея куплена · −$50";
            if (text.StartsWith("Delivered A", StringComparison.Ordinal)) return "Заказ A · +$260";
            if (text.StartsWith("Delivered B", StringComparison.Ordinal)) return "Заказ Б · +$125";
            if (text.StartsWith("Accepted ", StringComparison.Ordinal)) return "Заказ принят";
            if (text.StartsWith("Order ", StringComparison.Ordinal)) return "Заказ закрыт навсегда";
            return text switch
            {
                "Crate returned to its dock." => "Ящик на площадке",
                "Crate in hands. All stored lamps move with it." => "Ящик в руках",
                "Lamp retrieved from bench." => "Фонарь в руках",
                "Take a lamp from the crate first." => "Сначала возьми фонарь из ящика",
                "Lamp placed on bench. Run the test." => "Фонарь на столе · нужен тест",
                "Dock the crate first." => "Сначала выброси ящик из рук: Q.",
                "Place a lamp on the bench first." => "Возьми фонарь из ящика. E у рабочего стола — положить фонарь.",
                "Unknown lamp." => "Такого фонаря нет.",
                "Every lamp must have a different owned identity." => "Выбери разные принадлежащие тебе фонари.",
                "Repair time limit reached." => "Лимит времени ремонта достигнут.",
                _ => FirstDealText.Message(text)
            };
        }

        private Camera neighbourhoodCamera;
        private RenderTexture neighbourhoodTexture;
        private Rect neighbourhoodBounds;
        private bool wholeCityMap;
        private Button mapCloseButton;
        private Sprite mapPlayerArrow;
        private Texture2D mapPlayerArrowTexture;

        private Vector2 MapPosition(float x, float z)
        {
            var bounds = neighbourhoodBounds;
            return new Vector2((x - bounds.xMin) / bounds.width, (z - bounds.yMin) / bounds.height);
        }

        private void PrepareNeighbourhoodCamera()
        {
            var bounds = sceneBindings.MapBounds;
            // Include the surrounding street junctions, not just the trading points.
            neighbourhoodBounds = UnityEngine.Rect.MinMaxRect(bounds.xMin - 60, bounds.yMin - 60, bounds.xMax + 60, bounds.yMax + 60);
            if (wholeCityMap)
            {
                foreach (var sceneRoot in gameObject.scene.GetRootGameObjects())
                    foreach (var renderer in sceneRoot.GetComponentsInChildren<Renderer>())
                        if (renderer.name.StartsWith("Continuous ground", StringComparison.Ordinal))
                        {
                            var ground = renderer.bounds;
                            neighbourhoodBounds = UnityEngine.Rect.MinMaxRect(ground.min.x, ground.min.z, ground.max.x, ground.max.z);
                        }
            }
            else if (!neighbourhoodBounds.Contains(new Vector2(transform.position.x, transform.position.z)))
                neighbourhoodBounds.center = new Vector2(transform.position.x, transform.position.z);

            var aspect = neighbourhoodBounds.width / neighbourhoodBounds.height;
            var height = Mathf.RoundToInt(1536 / aspect);
            if (neighbourhoodTexture == null || neighbourhoodTexture.height != height)
            {
                if (neighbourhoodCamera != null) neighbourhoodCamera.targetTexture = null;
                if (neighbourhoodTexture != null) { neighbourhoodTexture.Release(); Destroy(neighbourhoodTexture); }
                neighbourhoodTexture = new RenderTexture(1536, height, 24) { name = "Neighbourhood terrain" };
                neighbourhoodTexture.Create();
            }
            if (neighbourhoodCamera == null)
            {
                var cameraObject = new GameObject("Neighbourhood map camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
                cameraObject.transform.SetParent(root, false);
                neighbourhoodCamera = cameraObject.GetComponent<Camera>();
                neighbourhoodCamera.orthographic = true;
                neighbourhoodCamera.clearFlags = CameraClearFlags.SolidColor;
                neighbourhoodCamera.backgroundColor = SlotPaper;
                neighbourhoodCamera.nearClipPlane = .1f; neighbourhoodCamera.farClipPlane = 180;
                neighbourhoodCamera.cullingMask = eye.cullingMask & ~(1 << 2);
                neighbourhoodCamera.useOcclusionCulling = false;
                neighbourhoodCamera.allowHDR = false; neighbourhoodCamera.allowMSAA = false;
                var data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
            }
            neighbourhoodCamera.targetTexture = neighbourhoodTexture;
            neighbourhoodCamera.aspect = aspect;
            neighbourhoodCamera.orthographicSize = neighbourhoodBounds.height * .5f;
            neighbourhoodCamera.transform.SetPositionAndRotation(new Vector3(neighbourhoodBounds.center.x, 120, neighbourhoodBounds.center.y),
                Quaternion.Euler(90, 0, 0));
            neighbourhoodCamera.enabled = false;
            DrawCartographicMap();
        }

        private void BuildLocalMap()
        {
            PrepareNeighbourhoodCamera();
            var controls = Rect("Map controls", menuContent, Vector2.zero, Vector2.one);
            controls.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 * textScale;
            var controlsLayout = controls.gameObject.AddComponent<HorizontalLayoutGroup>();
            controlsLayout.spacing = 8;
            controlsLayout.childControlWidth = controlsLayout.childControlHeight = true;
            ActionButton(controls, "Инвентарь", () => { showInventoryMap = false; RebuildMenu(); });
            ActionButton(controls, wholeCityMap ? "Приблизить район" : "Весь город", () => { wholeCityMap = !wholeCityMap; RebuildMenu(); });
            mapCloseButton = ActionButton(controls, "Закрыть карту", CloseMenu);
            foreach (var button in controls.GetComponentsInChildren<Button>())
            {
                var colors = button.colors; colors.normalColor = Color.clear;
                colors.highlightedColor = colors.selectedColor = new Color(.64f, .86f, .73f, .12f);
                button.colors = colors;
            }
            var frame = Rect("Map viewport", menuContent, Vector2.zero, Vector2.one);
            frame.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(140,
                Mathf.Min(400, safeArea.rect.height * .53f, safeArea.rect.height * .88f - 156 - 60 * textScale));
            var map = Rect("Карта района", frame, Vector2.zero, Vector2.one);
            var image = map.gameObject.AddComponent<RawImage>(); image.texture = neighbourhoodTexture;
            var fit = map.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = neighbourhoodBounds.width / neighbourhoodBounds.height;
            var labelPositions = new System.Collections.Generic.List<UnityEngine.Rect>();
            foreach (var site in sceneBindings.Sites)
            {
                var position = site.transform.position;
                if (wholeCityMap && site.Role != CityTradeKind.Workshop) continue;
                if (!neighbourhoodBounds.Contains(new Vector2(position.x, position.z))) continue;
                MapLabel(map, Title(site.Role), position.x, position.z, labelPositions);
            }
            var p = transform.position;
            if (neighbourhoodBounds.Contains(new Vector2(p.x, p.z)))
            {
                var marker = Rect("Ты здесь", map, MapPosition(p.x, p.z), MapPosition(p.x, p.z));
                if (mapPlayerArrow == null)
                {
                    mapPlayerArrowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                    for (var y = 0; y < 32; y++)
                        for (var x = 0; x < 32; x++)
                            mapPlayerArrowTexture.SetPixel(x, y, y >= 5 && y <= 29 && Mathf.Abs(x - 15.5f) <= (29 - y) * .45f ? Color.white : Color.clear);
                    mapPlayerArrowTexture.Apply(false, true);
                    mapPlayerArrow = Sprite.Create(mapPlayerArrowTexture, new UnityEngine.Rect(0, 0, 32, 32), Vector2.one * .5f);
                }
                marker.sizeDelta = new Vector2(22, 22);
                marker.localRotation = Quaternion.Euler(0, 0, -transform.eulerAngles.y);
                var arrow = marker.gameObject.AddComponent<Image>(); arrow.sprite = mapPlayerArrow;
                arrow.color = new Color(.9f, .5f, .07f);
            }
            Line("Север — вверху · жёлтая метка — ты", 16);
        }

        private void MapLabel(Transform map, string value, float x, float z, System.Collections.Generic.List<UnityEngine.Rect> occupied)
        {
            Canvas.ForceUpdateCanvases();
            var label = Label(map, value, 12); label.color = Ink;
            var position = MapPosition(x, z);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = position;
            var size = new Vector2(label.preferredWidth + 8, 22 * textScale);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.sizeDelta = size;
            var mapRect = (RectTransform)map;
            var center = Vector2.Scale(position, mapRect.rect.size);
            var offset = Vector2.up * 14;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var candidate = new UnityEngine.Rect(center + offset - size * .5f, size);
                if (!occupied.Exists(other => other.Overlaps(candidate))) { occupied.Add(candidate); break; }
                offset.y += size.y * (attempt % 2 == 0 ? -1 : 1) * (attempt + 1);
            }
            label.rectTransform.anchoredPosition = offset;
            label.rectTransform.anchoredPosition = new Vector2(
                Mathf.Clamp(center.x + offset.x, size.x * .5f, mapRect.rect.width - size.x * .5f) - center.x,
                Mathf.Clamp(center.y + offset.y, size.y * .5f, mapRect.rect.height - size.y * .5f) - center.y);
        }

        private void DisposeNeighbourhoodMap()
        {
            if (neighbourhoodCamera != null) { neighbourhoodCamera.targetTexture = null; Destroy(neighbourhoodCamera.gameObject); }
            if (neighbourhoodTexture != null) { neighbourhoodTexture.Release(); Destroy(neighbourhoodTexture); }
            DisposeCartography();
            Destroy(mapPlayerArrow); Destroy(mapPlayerArrowTexture);
        }
    }
}
