using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // A single responsive UI mesh keeps the schematic connected when its container resizes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CityTradeMapPaths : MaskableGraphic
    {
        private readonly List<Vector2[]> paths = new List<Vector2[]>();

        public void AddPath(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector2((coordinates[i * 2] + 105) / 220, (coordinates[i * 2 + 1] + 70) / 135);
            paths.Add(points);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            foreach (var path in paths)
                for (var i = 1; i < path.Length; i++)
                {
                    var a = bounds.min + Vector2.Scale(path[i - 1], bounds.size);
                    var b = bounds.min + Vector2.Scale(path[i], bounds.size);
                    var direction = (b - a).normalized;
                    var normal = new Vector2(-direction.y, direction.x) * 2;
                    var first = mesh.currentVertCount;
                    mesh.AddVert(a - normal, color, Vector2.zero);
                    mesh.AddVert(a + normal, color, Vector2.zero);
                    mesh.AddVert(b + normal, color, Vector2.zero);
                    mesh.AddVert(b - normal, color, Vector2.zero);
                    mesh.AddTriangle(first, first + 1, first + 2);
                    mesh.AddTriangle(first, first + 2, first + 3);
                }
        }
    }
}
