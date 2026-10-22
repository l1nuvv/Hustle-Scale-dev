using UnityEngine;
using UnityEngine.Rendering;

namespace Interactables
{
    public sealed partial class SolderingTrial
    {
        // Visual-only mesh snapshots. No cloned components, colliders, lights or items.
        private sealed class PlacementGhost
        {
            private readonly Mesh[] meshes;
            private readonly Matrix4x4[] localMatrices;
            private readonly Vector3 scale;

            public PlacementGhost(Transform source)
            {
                var filters = source.GetComponentsInChildren<MeshFilter>();
                meshes = new Mesh[filters.Length];
                localMatrices = new Matrix4x4[filters.Length];
                scale = source.lossyScale;
                for (var i = 0; i < filters.Length; i++)
                {
                    meshes[i] = filters[i].sharedMesh;
                    localMatrices[i] = source.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
                }
            }

            public void Draw(Vector3 position, Quaternion rotation, Camera camera, Material fill, Material mask, Material edge, MaterialPropertyBlock colours)
            {
                var pose = Matrix4x4.TRS(position, rotation, scale);
                for (var i = 0; i < meshes.Length; i++)
                    for (var sub = 0; sub < meshes[i].subMeshCount; sub++)
                    {
                        var matrix = pose * localMatrices[i];
                        Graphics.DrawMesh(meshes[i], matrix, fill, 0, camera, sub, colours, ShadowCastingMode.Off, false);
                        Graphics.DrawMesh(meshes[i], matrix, mask, 0, camera, sub, null, ShadowCastingMode.Off, false);
                        Graphics.DrawMesh(meshes[i], matrix, edge, 0, camera, sub, colours, ShadowCastingMode.Off, false);
                    }
            }
        }

        private PlacementGhost flashlightGhost, coverGhost, ironGhost, solderGhost, currentGhost;
        private Material ghostFill, ghostMask, ghostEdge;
        private Quaternion ghostRotation;
        private PlacementGhost alternateGhost;
        private Vector3 alternatePosition;
        private MaterialPropertyBlock ghostColours, alternateColours;
        public bool PlacementPreviewVisible => currentGhost != null;
        public bool AlternatePlacementVisible => alternateGhost != null;
        public RepairTarget PlacementPreviewTarget { get; private set; }
        public Vector3 PlacementPreviewPosition { get; private set; }
        public bool PlacementReady { get; private set; }

        private void BuildPlacementPreviews()
        {
            ghostFill = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ghostFill.SetFloat("_Surface", 1);
            ghostFill.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            ghostFill.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            ghostFill.SetFloat("_ZWrite", 0);
            ghostFill.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostFill.renderQueue = 3007;
            ghostMask = new Material(Resources.Load<Shader>("PickupOutlineMask"));
            ghostEdge = new Material(Resources.Load<Shader>("PickupOutline"));
            ghostEdge.SetFloat("_OutlinePixels", 2.3f);
            resources.Add(ghostFill); resources.Add(ghostMask); resources.Add(ghostEdge);
            ghostColours = new MaterialPropertyBlock();
            alternateColours = new MaterialPropertyBlock();
            SetPreviewColours(alternateColours, false);
            ironGhost = new PlacementGhost(iron);
            solderGhost = new PlacementGhost(solder);
            if (!fullOperation) return;
            flashlightGhost = new PlacementGhost(flashlight);
            coverGhost = new PlacementGhost(cover);
        }

        private void RefreshPlacementPreview()
        {
            currentGhost = null;
            alternateGhost = null;
            PlacementPreviewTarget = RepairTarget.None;
            PlacementReady = false;
            if (!active || !focused || !Application.isFocused || Time.timeScale <= 0 || IsToolAnimating ||
                (fullOperation && (workPaused || RepairAnimating))) return;
            var position = Vector3.zero;
            ghostRotation = transform.rotation;
            if (state.HeldTool != SolderingTrialState.Tool.None)
            {
                var isIron = state.HeldTool == SolderingTrialState.Tool.Iron;
                currentGhost = isIron ? ironGhost : solderGhost;
                position = isIron ? IronDock : SolderDock;
                ghostRotation = transform.rotation * DockRotation(isIron ? ironAnchor : solderAnchor);
            }
            else if (fullOperation && repair.Flashlight == FlashlightRepairState.ItemPlace.Held)
            {
                currentGhost = flashlightGhost;
                position = Joint;
                PlacementPreviewTarget = RepairTarget.WorkMat;
            }
            else if (fullOperation && repair.Cover == FlashlightRepairState.CoverPlace.Held)
            {
                currentGhost = coverGhost;
                PlacementPreviewTarget = repairHover == RepairTarget.WorkMat || repairHover == RepairTarget.Tray
                    ? repairHover : repair.ReturningCover ? RepairTarget.WorkMat : RepairTarget.Tray;
                position = PlacementPreviewTarget == RepairTarget.WorkMat ? Joint : CoverTray;
                alternateGhost = coverGhost;
                alternatePosition = transform.TransformPoint(PlacementPreviewTarget == RepairTarget.WorkMat ? CoverTray : Joint);
            }
            if (currentGhost == null) return;
            PlacementPreviewPosition = transform.TransformPoint(position);
            PlacementReady = PlacementPreviewTarget != RepairTarget.None && repairHover == PlacementPreviewTarget;
            var colour = PlacementReady ? new Color(0.25f, 1f, 0.55f) : new Color(0.18f, 0.75f, 1f);
            ghostEdge.SetColor("_OutlineColor", colour);
            colour.a = PlacementReady ? 0.23f : 0.13f;
            ghostFill.SetColor("_BaseColor", colour);
            SetPreviewColours(ghostColours, PlacementReady);
        }

        private static void SetPreviewColours(MaterialPropertyBlock block, bool ready)
        {
            var colour = ready ? new Color(0.25f, 1f, 0.55f) : new Color(0.18f, 0.75f, 1f);
            block.SetColor("_OutlineColor", colour);
            colour.a = ready ? 0.23f : 0.13f;
            block.SetColor("_BaseColor", colour);
        }

        private void DrawPlacementPreview()
        {
            alternateGhost?.Draw(alternatePosition, transform.rotation, view, ghostFill, ghostMask, ghostEdge, alternateColours);
            currentGhost?.Draw(PlacementPreviewPosition, ghostRotation, view, ghostFill, ghostMask, ghostEdge, ghostColours);
        }
    }
}
