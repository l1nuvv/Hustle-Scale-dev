using System;
using Player;
using UnityEditor;
using UnityEngine;

public static class CityFixtureAuthoring
{
    [MenuItem("Tools/City/Validate Scene Bindings")]
    public static void ValidateScene()
    {
        var contexts = UnityEngine.Object.FindObjectsByType<CitySceneBindings>(FindObjectsSortMode.None);
        if (contexts.Length != 1 || !contexts[0].ValidateConfiguration())
            throw new InvalidOperationException("City configuration invalid; see object-specific Console diagnostics.");
        Debug.Log("City scene configuration PASS");
    }
}
