using UnityEngine;
using UnityEngine.Rendering;

namespace Interactables
{
    // One focused object, shared by ordinary ray interaction and the M2 tool cursor.
    // Original renderers/materials are never changed. Depth-tested hulls cannot show through walls.
    [DefaultExecutionOrder(300)]
    public sealed class PickupOutline : MonoBehaviour
    {
        private Transform target;
        private Camera eye;
        private MeshFilter[] parts;
        private Material material;
        private Material mask;
        public Transform Target => target;

        public void Focus(Transform next, Camera camera)
        {
            eye = camera;
            if (ReferenceEquals(target, next)) return;
            target = next;
            parts = target != null ? target.GetComponentsInChildren<MeshFilter>() : null;
            if (target == null || material != null) return;
            var shader = Resources.Load<Shader>("PickupOutline");
            var maskShader = Resources.Load<Shader>("PickupOutlineMask");
            if (shader == null || maskShader == null) { Debug.LogError("Pickup outline shader missing.", this); return; }
            material = new Material(shader) { name = "Focused pickup - amber outline" };
            mask = new Material(maskShader) { name = "Focused pickup - visible silhouette mask" };
            material.SetColor("_OutlineColor", new Color(1f, 0.72f, 0.20f, 1));
            material.SetFloat("_OutlinePixels", 2.5f);
        }

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy || eye == null || material == null || parts == null) return;
            foreach (var part in parts)
            {
                if (part == null || !part.gameObject.activeInHierarchy || part.sharedMesh == null || part.GetComponent<TextMesh>() != null) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff) continue;
                for (var i = 0; i < part.sharedMesh.subMeshCount; i++)
                {
                    // All masks render before all hulls, hiding interior seams between prop parts.
                    Graphics.DrawMesh(part.sharedMesh, part.transform.localToWorldMatrix, mask,
                        part.gameObject.layer, eye, i, null, ShadowCastingMode.Off, false);
                    Graphics.DrawMesh(part.sharedMesh, part.transform.localToWorldMatrix, material,
                        part.gameObject.layer, eye, i, null, ShadowCastingMode.Off, false);
                }
            }
        }

        private void OnDisable() => Focus(null, eye);
        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mask != null) Destroy(mask);
        }
    }
}
