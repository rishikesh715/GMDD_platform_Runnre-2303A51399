#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GearRunnerMenu
{
    [MenuItem("Gear Runner/Create Game Scene")]
    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("GearRunner").AddComponent<GearRunner>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GearRunner.unity");
        AssetDatabase.Refresh();
        Debug.Log("Gear Runner scene created at Assets/Scenes/GearRunner.unity - press Play!");
    }
}
#endif
