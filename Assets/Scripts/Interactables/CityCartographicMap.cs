using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private readonly Dictionary<int, Material> cartographicMaterials = new Dictionary<int, Material>();
        private Mesh mapContours;
        private static readonly Color[] MapPalette =
        {
            new Color(.87f, .86f, .81f), // Existing ground.
            new Color(.985f, .98f, .95f), // Streets.
            new Color(.93f, .92f, .87f), // Pavements and yards.
            new Color(.77f, .79f, .76f), // Building footprints.
            new Color(.66f, .74f, .59f), // Existing planted surfaces only.
            new Color(.65f, .64f, .57f), // Small existing structures.
            new Color(.58f, .63f, .65f), // Vehicles.
            new Color(.65f, .63f, .56f), // Road markings.
            new Color(.54f, .58f, .55f) // Roof contours.
        };

        private Material MapMaterial(int kind)
        {
            if (cartographicMaterials.TryGetValue(kind, out var material)) return material;
            material = new Material(Resources.Load<Shader>("CityCartographicMap"));
            material.SetColor("_MapColor", MapPalette[kind]);
            material.SetInt("_MapZTest", (int)CompareFunction.Always);
            if (kind <= 4)
            {
                var surfaces = Resources.Load<Texture2D>("CityMapSurfaces");
                if (surfaces == null) throw new InvalidOperationException("CityMapSurfaces atlas is missing.");
                material.SetTexture("_MapTexture", surfaces);
                var tile = kind == 1 ? new Vector4(0, .5f, .5f, .5f) :
                    kind == 3 ? new Vector4(0, 0, .5f, .5f) :
                    kind == 4 ? new Vector4(.5f, 0, .5f, .5f) : new Vector4(.5f, .5f, .5f, .5f);
                material.SetVector("_MapTile", tile);
                material.SetFloat("_MapScale", kind == 2 || kind == 0 ? 8 : 16);
                material.SetFloat("_MapDetail", .8f);
            }
            cartographicMaterials.Add(kind, material);
            return material;
        }

        private static string MapObjectPath(Transform target)
        {
            var path = target.name;
            for (var parent = target.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path.ToLowerInvariant();
        }

        private static int MapFeature(MeshRenderer renderer, string path)
        {
            var name = renderer.name.ToLowerInvariant();
            var materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name.ToLowerInvariant() : "";
            if (name.Contains("continuous ground")) return 0;
            if (name.Contains("dashed line") || name.Contains("road marking")) return 7;
            if (name.StartsWith("tree", StringComparison.Ordinal) || materialName.Contains("park") || materialName.Contains("grass")) return 4;
            if (path.Contains("vehicle")) return 6;
            if (path.Contains("building |") || name.Contains("roof") || name.Contains("wall")) return 3;
            if (materialName.Contains("asphalt") || name.Contains("street") && !name.Contains("sidewalk") && !name.Contains("curb")) return 1;
            if (materialName.Contains("pavement") || name.Contains("pavement") || name.Contains("sidewalk") || name.Contains("yard") || name.Contains("courtyard") || name.Contains("curb")) return 2;
            return 5;
        }

        // Draw the scene's actual meshes with a map palette. No authored replacement
        // roads, building stamps, decorative parks or guessed geography are inserted.
        private void DrawCartographicMap()
        {
            var command = new CommandBuffer { name = "Draw exact city plan" };
            var contourVertices = new List<Vector3>(); var contourTriangles = new List<int>();
            try
            {
                command.SetRenderTarget(neighbourhoodTexture);
                command.SetViewport(new UnityEngine.Rect(0, 0, neighbourhoodTexture.width, neighbourhoodTexture.height));
                command.ClearRenderTarget(true, true, MapPalette[0], SystemInfo.usesReversedZBuffer ? 0 : 1);
                var projection = GL.GetGPUProjectionMatrix(neighbourhoodCamera.projectionMatrix, true) * neighbourhoodCamera.worldToCameraMatrix;
                var properties = new MaterialPropertyBlock();
                foreach (var renderer in gameObject.scene.GetRootGameObjects().SelectMany(sceneRoot => sceneRoot.GetComponentsInChildren<MeshRenderer>())
                    .OrderBy(renderer => renderer.bounds.max.y))
                {
                    if (!renderer.enabled || renderer.GetComponentInParent<Canvas>() != null || renderer.gameObject.layer == 2) continue;
                    var bounds = renderer.bounds;
                    if (bounds.max.x < neighbourhoodBounds.xMin || bounds.min.x > neighbourhoodBounds.xMax ||
                        bounds.max.z < neighbourhoodBounds.yMin || bounds.min.z > neighbourhoodBounds.yMax) continue;
                    var name = renderer.name.ToLowerInvariant();
                    // Vertical text and window strips are not geographic features.
                    if (name.Contains("sign") || name.Contains("letter") || name.Contains("window") || name.Contains("label")) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var path = MapObjectPath(renderer.transform);
                    var kind = MapFeature(renderer, path);
                    command.SetGlobalMatrix("_MapMatrix", projection * renderer.localToWorldMatrix);
                    command.SetGlobalMatrix("_MapWorldMatrix", renderer.localToWorldMatrix);
                    // DrawRenderer preserves each renderer's own range inside static batches.
                    for (var submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++)
                        command.DrawRenderer(renderer, MapMaterial(kind), submesh, 0);
                    if (name.Contains("roof")) AddRoofContour(renderer, contourVertices, contourTriangles);
                }
                if (mapContours != null) Destroy(mapContours);
                mapContours = new Mesh { name = "Scene roof contours" };
                mapContours.SetVertices(contourVertices); mapContours.SetTriangles(contourTriangles, 0);
                properties.SetMatrix("_MapMatrix", projection);
                command.DrawMesh(mapContours, Matrix4x4.identity, MapMaterial(8), 0, 0, properties);
                Graphics.ExecuteCommandBuffer(command);
            }
            finally { command.Release(); }
        }

        private static void AddRoofContour(MeshRenderer renderer, List<Vector3> vertices, List<int> triangles)
        {
            var bounds = renderer.localBounds;
            var top = bounds.max.y + .025f;
            var corners = new[] { new Vector3(bounds.min.x, top, bounds.min.z), new Vector3(bounds.max.x, top, bounds.min.z),
                new Vector3(bounds.max.x, top, bounds.max.z), new Vector3(bounds.min.x, top, bounds.max.z) };
            for (var i = 0; i < 4; i++) corners[i] = renderer.transform.TransformPoint(corners[i]);
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                var normal = Vector3.Cross((b - a).normalized, Vector3.up) * .22f;
                var first = vertices.Count;
                vertices.Add(a - normal); vertices.Add(a + normal); vertices.Add(b + normal); vertices.Add(b - normal);
                // Both faces: roof winding depends on the source object's transform.
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first);
                triangles.Add(first + 3); triangles.Add(first + 2); triangles.Add(first);
            }
        }

        private void DisposeCartography()
        {
            foreach (var material in cartographicMaterials.Values) Destroy(material);
            cartographicMaterials.Clear();
            if (mapContours != null) Destroy(mapContours);
        }
    }
}
