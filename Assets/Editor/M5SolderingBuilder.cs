using System;
using Interactables;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class M5SolderingBuilder
{
    public const string PrefabPath = "Assets/Art/SolderingTrial/Prefabs/CitySolderingStation.prefab";
    [MenuItem("Tools/M5/Install City Station")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save the current scene first.");
        var holder = new GameObject("City soldering station");
        holder.SetActive(false);
        GameObject prefab;
        try
        {
            var trial = holder.AddComponent<SolderingTrial>();
            var data = new SerializedObject(trial);
            data.FindProperty("fullOperation").boolValue = true;
            foreach (var pair in new[] { ("ironVisualPrefab", "M2_SolderingIron"), ("solderVisualPrefab", "M2_SolderSpool"),
                ("flashlightVisualPrefab", "M3_Flashlight"), ("solderingMatPrefab", "SolderingMat") })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SolderingTrial/Prefabs/" + pair.Item2 + ".prefab");
                if (asset == null) throw new InvalidOperationException("Missing adopted visual: " + pair.Item2);
                data.FindProperty(pair.Item1).objectReferenceValue = asset;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            prefab = PrefabUtility.SaveAsPrefabAsset(holder, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(holder); }
        var city = EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        var walk = UnityEngine.Object.FindAnyObjectByType<CityGreyboxWalk>();
        var settings = new SerializedObject(walk);
        settings.FindProperty("solderingStationPrefab").objectReferenceValue = prefab;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(city); EditorSceneManager.SaveScene(city); AssetDatabase.SaveAssets();
        Debug.Log("M5: station prefab installed with adopted M3 visuals; no gameplay purchase performed.");
    }
}
