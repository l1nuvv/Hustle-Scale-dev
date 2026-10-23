using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Flat map symbols, using the same simple shapes as the game's iconography.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CityMapIcon : MaskableGraphic
    {
        private CityTradeKind role;
        private static readonly Color Blue = new Color(.21f, .37f, .45f);
        private static readonly Color Brass = new Color(.72f, .51f, .21f);
        private static readonly Color Cream = new Color(.94f, .91f, .85f);

        public void Configure(CityTradeKind value) { role = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (role == CityTradeKind.Workshop)
            {
                Quad(mesh, 5, 4, 19, 16, Cream);
                Triangle(mesh, new Vector2(2, 15), new Vector2(12, 23), new Vector2(22, 15), Blue);
                Quad(mesh, 6, 3, 18, 5, Blue);
                Stroke(mesh, new Vector2(9, 7), new Vector2(16, 14), 2.5f, Brass);
                Quad(mesh, 14, 12, 18, 16, Brass);
                Triangle(mesh, new Vector2(14, 16), new Vector2(16, 14), new Vector2(14, 14), Cream);
            }
            else if (role == CityTradeKind.Seller)
            {
                Quad(mesh, 4, 3, 6, 16, Blue); Quad(mesh, 18, 3, 20, 16, Blue);
                Quad(mesh, 3, 3, 21, 9, Blue);
                Quad(mesh, 2, 15, 22, 21, Blue);
                Quad(mesh, 6, 15, 10, 21, Cream); Quad(mesh, 14, 15, 18, 21, Cream);
                Quad(mesh, 9, 9, 15, 12, Brass);
            }
            else
            {
                Quad(mesh, 3, 4, 16, 15, Blue);
                Quad(mesh, 8, 11, 11, 15, Cream);
                Stroke(mesh, new Vector2(12, 20), new Vector2(22, 20), 2.5f, Brass);
                Triangle(mesh, new Vector2(12, 16), new Vector2(7, 20), new Vector2(12, 24), Brass);
            }
        }

        private Vector2 Point(Vector2 point) => rectTransform.rect.min + Vector2.Scale(point / 24, rectTransform.rect.size);

        private void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color tint)
        {
            var first = mesh.currentVertCount;
            mesh.AddVert(Point(new Vector2(left, bottom)), tint, Vector2.zero);
            mesh.AddVert(Point(new Vector2(left, top)), tint, Vector2.zero);
            mesh.AddVert(Point(new Vector2(right, top)), tint, Vector2.zero);
            mesh.AddVert(Point(new Vector2(right, bottom)), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            var first = mesh.currentVertCount;
            mesh.AddVert(Point(a), tint, Vector2.zero); mesh.AddVert(Point(b), tint, Vector2.zero); mesh.AddVert(Point(c), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Triangle(mesh, a - normal, a + normal, b + normal, tint);
            Triangle(mesh, a - normal, b + normal, b - normal, tint);
        }
    }
}
