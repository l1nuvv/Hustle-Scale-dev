using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class FlashlightRepairVerification
{
    private static int checks;
    private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    private const string LogPath = "Logs/Codex-M3-verification.log";

    [MenuItem("Tools/Development Sandbox/Install M3 Once")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != DevelopmentSandboxBuilder.ScenePath)
            throw new InvalidOperationException("Open saved DevelopmentSandbox outside Play Mode.");
        if (UnityEngine.Object.FindObjectsByType<SolderingTrial>().Any(t => t.IsFullOperation))
            throw new InvalidOperationException("M3 already installed.");
        var prefab = BuildPrefab();
        var m2 = UnityEngine.Object.FindObjectsByType<SolderingTrial>().Single(t => !t.IsFullOperation);
        var station = UnityEngine.Object.Instantiate(m2.gameObject);
        station.name = "M3 - Full flashlight repair - zone 06";
        station.transform.position = new Vector3(3, 0, -48);
        var data = new SerializedObject(station.GetComponent<SolderingTrial>());
        data.FindProperty("fullOperation").boolValue = true;
        data.FindProperty("flashlightVisualPrefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("M3 installed beside M2, centre (3, 0, -48). E6.6 remains open.");
    }

    [MenuItem("Tools/Development Sandbox/Update M3 Visual")]
    public static void UpdateVisual()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        BuildPrefab();
        AssetDatabase.SaveAssets();
    }

    private static GameObject BuildPrefab()
    {
        const string art = "Assets/Art/SolderingTrial/";
        var importer = (ModelImporter)AssetImporter.GetAtPath(art + "Models/M3_Flashlight.fbx");
        if (importer == null) throw new InvalidOperationException("Export M3 Blender variant first.");
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.globalScale = 1;
        importer.SaveAndReimport();
        var wrapper = new GameObject("M3 Flashlight Visual");
        GameObject prefab;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(art + "Models/M3_Flashlight.fbx"));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(wrapper.transform, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(art + "Materials/M2_SurfaceAtlas.mat");
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            var meshes = model.GetComponentsInChildren<MeshRenderer>();
            var lid = new GameObject("M3_Cover").transform;
            lid.SetParent(wrapper.transform, false);
            var fastener0 = new GameObject("M3_Fastener0").transform;
            fastener0.SetParent(lid, false); fastener0.localPosition = new Vector3(-0.24f, 0.067f, -0.088f);
            var fastener1 = new GameObject("M3_Fastener1").transform;
            fastener1.SetParent(lid, false); fastener1.localPosition = new Vector3(0.17f, 0.067f, -0.088f);
            var power = new GameObject("M3_PowerSwitch").transform;
            power.SetParent(lid, false); power.localPosition = new Vector3(-0.10f, 0.070f, -0.080f);
            foreach (var mesh in meshes)
            {
                if (mesh.name.StartsWith("M3Body_", StringComparison.Ordinal)) continue;
                var parent = mesh.name.StartsWith("M3 captive", StringComparison.Ordinal) || mesh.name.StartsWith("M3 thumb", StringComparison.Ordinal)
                    ? mesh.name.EndsWith(".001", StringComparison.Ordinal) ? fastener1 : fastener0
                    : mesh.name.StartsWith("M3 power", StringComparison.Ordinal) ? power : lid;
                mesh.transform.SetParent(parent, true);
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, art + "Prefabs/M3_Flashlight.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        return prefab;
    }

    [MenuItem("Tools/Development Sandbox/Verify M3 State")]
    public static void VerifyState()
    {
        Begin();
        try
        {
            var solder = new SolderingTrialState();
            var repair = new FlashlightRepairState(solder);
            Check(!solder.Accessible && !repair.LightOn && !repair.Complete, "initial closed broken item blocks access and light");
            Check(!repair.PlaceFlashlight() && !repair.LiftCover() && !repair.ReplaceCover() && !repair.TogglePower(), "cannot skip pickup, placement, fasteners or assembly");
            Check(repair.TakeFlashlight() && !repair.TakeFlashlight() && !repair.TurnFastener(0), "one held item, no work before placement");
            Check(repair.PlaceFlashlight() && !repair.PlaceFlashlight(), "place exactly once");
            Check(repair.TogglePower() && !repair.LightOn && !repair.Complete, "broken assembled flashlight cannot light");
            solder.SelectTool(SolderingTrialState.Tool.Solder);
            solder.Tick(1, true, true);
            Check(!solder.HasSolder && !repair.TurnFastener(0), "closed body rejects preparation and occupied hand rejects fastener");
            solder.SelectTool(SolderingTrialState.Tool.Iron);
            solder.Tick(4, true, true);
            solder.Tick(10, true, true);
            Check(solder.Progress == 0, "hot iron cannot solder through closed body");
            solder.SelectTool(SolderingTrialState.Tool.None);
            Check(repair.TurnFastener(0) && !repair.LiftCover() && !repair.TogglePower(), "one fastener is insufficient; loose body cannot be tested");
            Check(!repair.TurnFastener(-1) && !repair.TurnFastener(2), "invalid fasteners rejected");
            repair.TurnFastener(1);
            Check(repair.LiftCover() && !repair.LiftCover() && !solder.Accessible, "single held cover; tools wait for tray placement");
            Check(repair.PutCoverInTray() && !repair.PutCoverInTray() && solder.Accessible, "one cover in visible tray grants access");
            Check(repair.TakeCover() && repair.ReplaceCover() && !repair.ReplaceCover() && !solder.Accessible, "broken joint survives reassembly without repair or duplicate lid");
            repair.TurnFastener(0); repair.TurnFastener(1);
            repair.TogglePower();
            Check(!repair.LightOn && solder.Progress == 0 && !repair.Complete, "cover alone does not fix connection");
            Open(repair);
            solder.SelectTool(SolderingTrialState.Tool.Iron);
            solder.Tick(1, true, true);
            Check(solder.Progress == 0, "hot iron still requires solder");
            solder.SelectTool(SolderingTrialState.Tool.Solder);
            solder.Tick(0.1f, false, true);
            Check(!solder.HasSolder, "solder requires physical contact");
            solder.Tick(0.1f, true, true);
            solder.SelectTool(SolderingTrialState.Tool.Iron);
            solder.Tick(1, true, true);
            var partial = solder.Progress;
            Check(partial > 0 && partial < 1 && solder.Working, "hot prepared contact advances partially");
            solder.Tick(10, false, true);
            solder.Tick(10, true, false);
            Check(solder.Progress == partial && !solder.Working, "withdrawal and pause preserve partial progress without work");
            Check(!repair.TakeCover() && !repair.TogglePower(), "tool must be returned before assembly/test");
            solder.Tick(2.5f, true, true);
            solder.Tick(10, true, true);
            Check(solder.Complete && solder.CompletionCount == 1 && !repair.Complete, "solder completion is once, not full operation success");
            solder.SelectTool(SolderingTrialState.Tool.None);
            Close(repair);
            Check(!repair.Complete && !repair.LightOn, "assembled repaired item still requires power test");
            Check(repair.TogglePower() && repair.LightOn && repair.Complete && repair.CompletionCount == 1, "assembly plus power test confirms operation");
            repair.TogglePower(); repair.TogglePower();
            Check(repair.CompletionCount == 1 && repair.LightOn, "repeated switch cannot repeat completion");
            repair.TurnFastener(0);
            Check(!repair.LightOn, "opening interrupts light");
            repair.Reset();
            Check(repair.Flashlight == FlashlightRepairState.ItemPlace.Supply && repair.Cover == FlashlightRepairState.CoverPlace.Attached &&
                repair.IsSecured(0) && repair.IsSecured(1) && !repair.LightOn && !repair.Tested && !repair.Complete &&
                !solder.Powered && !solder.HasSolder && solder.Progress == 0 && !solder.Accessible, "R restores complete initial closed broken state");
            repair.TakeFlashlight(); repair.PlaceFlashlight(); Open(repair);
            solder.SelectTool(SolderingTrialState.Tool.Solder); solder.Tick(0.1f, true, true);
            solder.SelectTool(SolderingTrialState.Tool.Iron); solder.Tick(4, false, true); solder.Tick(2.5f, true, true);
            solder.SelectTool(SolderingTrialState.Tool.None); Close(repair); repair.TogglePower();
            Check(repair.LightOn && repair.CompletionCount == 1 && solder.CompletionCount == 1, "entire sequence repeats after reset");
            Finish("STATE");
        }
        catch (Exception e) { Fail(e); throw; }
    }

    private static void Open(FlashlightRepairState r) { r.TurnFastener(0); r.TurnFastener(1); r.LiftCover(); r.PutCoverInTray(); }
    private static void Close(FlashlightRepairState r) { r.TakeCover(); r.ReplaceCover(); r.TurnFastener(0); r.TurnFastener(1); }
    private static void Begin() { checks = 0; report.Clear(); }
    private static void Check(bool ok, string message)
    {
        report.AppendLine((ok ? "PASS " : "FAIL ") + message);
        if (!ok) throw new InvalidOperationException(message);
        checks++;
    }
    private static void Finish(string mode)
    {
        report.AppendLine("M3 " + mode + ": PASS " + checks + " assertions; Unity " + Application.unityVersion);
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, report.ToString());
        Debug.Log("M3 " + mode + ": PASS " + checks + "; full log: " + LogPath);
    }
    private static void Fail(Exception e) { Directory.CreateDirectory("Logs"); File.AppendAllText(LogPath, report + "FAIL " + e + Environment.NewLine); }
}
