using System;
using System.IO;
using System.Linq;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class M7SharpeningBuilder
{
    public const string StationPath = "Assets/Art/SharpeningTrial/CitySharpeningStation.prefab";
    public const string KnifePath = "Assets/Art/SharpeningTrial/CityKnife.prefab";
    [MenuItem("Tools/M7/Install City Sharpening Once")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Preserve scene edits and stop Play Mode first.");
        EditorSceneManager.OpenScene("Assets/Scenes/DevelopmentSandbox.unity");
        var source = UnityEngine.Object.FindAnyObjectByType<SharpeningTrial>();
        var clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = "City sharpening station"; clone.SetActive(false);
        clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var data = new SerializedObject(clone.GetComponent<SharpeningTrial>());
        data.FindProperty("player").objectReferenceValue = null;
        data.FindProperty("view").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        var knifeRoot = (Transform)data.FindProperty("knife").objectReferenceValue;
        var carried = UnityEngine.Object.Instantiate(knifeRoot.gameObject);
        carried.name = "Kitchen knife"; carried.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        carried.transform.localScale = Vector3.one;
        // Reuse authored Blender material and silhouette; powder is applied by the work presentation.
        var powder = carried.GetComponentsInChildren<Renderer>().Single(r => r.name == "PowderCoating");
        UnityEngine.Object.DestroyImmediate(powder.gameObject);
        var knife = PrefabUtility.SaveAsPrefabAsset(carried, KnifePath);
        CreateIcon(carried);
        UnityEngine.Object.DestroyImmediate(carried);
        var label = clone.GetComponentInChildren<TextMesh>(true); label.text = "";
        var prefab = PrefabUtility.SaveAsPrefabAsset(clone, StationPath);
        UnityEngine.Object.DestroyImmediate(clone);
        var city = EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        if (UnityEngine.Object.FindAnyObjectByType<CitySharpeningSite>() != null) throw new InvalidOperationException("M7 already installed.");
        var bindings = UnityEngine.Object.FindAnyObjectByType<Player.CitySceneBindings>();
        bindings.ValidateConfiguration();
        var site = new GameObject("City sharpening site").AddComponent<CitySharpeningSite>();
        var solder = bindings.Station.transform;
        site.transform.SetPositionAndRotation(solder.position + solder.right * 2.2f, solder.rotation);
        site.Station = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, site.transform)).GetComponent<SharpeningTrial>();
        site.KnifePrefab = knife;
        site.Preview = Label(site.transform, "МЕСТО ЗАТОЧКИ\nСтанок $400 · у продавца", new Vector3(-.5f, 1.4f, .3f));
        site.Plaque = Label(site.transform, "ЗАТОЧКА", new Vector3(-.42f, 1.55f, .4f));
        site.Plaque.SetActive(false); site.Preview.SetActive(true);
        data = new SerializedObject(bindings.Trade);
        data.FindProperty("sharpeningSite").objectReferenceValue = site; data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(city); EditorSceneManager.SaveScene(city); AssetDatabase.SaveAssets();
        Debug.Log("M7 installed with adopted M6 Blender models at " + site.transform.position);
    }
    private static GameObject Label(Transform parent, string text, Vector3 position)
    {
        var label = new GameObject(text).AddComponent<TextMesh>(); label.transform.SetParent(parent, false);
        label.transform.localPosition = position; label.text = text; label.characterSize = .008f;
        label.fontSize = 48; label.color = new Color(.8f, .85f, .8f); return label.gameObject;
    }
    private static void CreateIcon(GameObject knife)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            preview.AddSingleGO(knife);
            preview.camera.transform.SetPositionAndRotation(new Vector3(.07f, .65f, -.12f), Quaternion.Euler(80, 0, 0));
            preview.camera.orthographic = true; preview.camera.orthographicSize = .26f;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 3;
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.clear;
            preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
            preview.BeginStaticPreview(new Rect(0, 0, 256, 256)); preview.camera.Render();
            var texture = preview.EndStaticPreview();
            File.WriteAllBytes("Assets/Resources/CityKnifeIcon.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { preview.Cleanup(); }
        AssetDatabase.ImportAsset("Assets/Resources/CityKnifeIcon.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/CityKnifeIcon.png");
        importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true; importer.SaveAndReimport();
    }
}
