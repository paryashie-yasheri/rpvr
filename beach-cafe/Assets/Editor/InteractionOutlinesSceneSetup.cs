using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class InteractionOutlinesSceneSetup
{
    [MenuItem("Beach Restaurant/Setup All Interaction Outlines")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var root = GameObject.Find("Interaction Outlines");
        if (!root) root = new GameObject("Interaction Outlines");
        var manager = root.GetComponent<InteractionOutlineManager>();
        if (!manager) manager = root.AddComponent<InteractionOutlineManager>();
        manager.interactionManager = Object.FindFirstObjectByType<XRInteractionManager>();
        foreach (var item in Object.FindObjectsByType<XRBaseInteractable>())
        {
            var graphics = HoverOutlineSetup.Configure(item.gameObject);
            manager.outlineMaterial = graphics.outlineMaterial;
            EditorUtility.SetDirty(item.gameObject);
        }
        EditorUtility.SetDirty(manager);
        // Keep the reusable drink prefab consistent with its scene instance.
        CounterModelsSetup.SavePrefab(GameObject.Find("Cup"), "Assets/BeachRestaurant/Models/SodaCup/Soda Cup.prefab");
        CounterModelsSetup.SavePrefab(GameObject.Find("Table Bell"), "Assets/BeachRestaurant/Models/TableBell/Table Bell.prefab");
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }
}
