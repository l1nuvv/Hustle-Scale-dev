using UnityEngine;

namespace Interactables
{
    // Legacy dynamic fonts can replace their atlas when a new glyph is requested.
    // Keep the depth-tested world material on that atlas without per-frame polling.
    [ExecuteAlways]
    public sealed class WorldSignFont : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Material material;

        private void OnEnable()
        {
            Font.textureRebuilt += Rebuilt;
            Refresh();
        }

        private void OnDisable() => Font.textureRebuilt -= Rebuilt;

        public void Bind(Font source, Material target)
        {
            font = source;
            material = target;
            Refresh();
        }

        private void Rebuilt(Font source) { if (source == font) Refresh(); }
        private void Refresh()
        {
            if (font != null && material != null) material.mainTexture = font.material.mainTexture;
        }
    }
}
