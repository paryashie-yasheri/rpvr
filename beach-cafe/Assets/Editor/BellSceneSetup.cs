using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class BellSceneSetup
{
    [MenuItem("Beach Restaurant/Setup Bell Interaction")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var bell = GameObject.Find("Table Bell");
        Configure(bell);
        CounterModelsSetup.SavePrefab(bell, "Assets/BeachRestaurant/Models/TableBell/Table Bell.prefab");
        EditorSceneManager.MarkSceneDirty(bell.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static void Configure(GameObject bell)
    {
        if (!bell) throw new System.InvalidOperationException("Table Bell is missing.");
        var audio = bell.GetComponent<AudioSource>();
        if (!audio) audio = bell.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.volume = 0.7f;
        audio.minDistance = 0.4f;
        audio.maxDistance = 8f;
        audio.rolloffMode = AudioRolloffMode.Logarithmic;
        audio.dopplerLevel = 0f;
        if (!audio.clip) audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BeachRestaurant/Audio/ServiceBell.wav");
        if (!audio.clip) throw new System.InvalidOperationException("Import ServiceBell.wav before configuring the bell.");

        var collider = bell.GetComponent<BoxCollider>();
        if (!collider) collider = bell.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = new Vector3(0, 0.065f, 0);
        collider.size = new Vector3(0.19f, 0.15f, 0.19f);
        var interactable = bell.GetComponent<XRSimpleInteractable>();
        if (!interactable) interactable = bell.AddComponent<XRSimpleInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(collider);
        foreach (var shape in bell.GetComponentsInChildren<MeshCollider>()) interactable.colliders.Add(shape);

        var button = bell.GetComponent<HoverPressButton>();
        if (!button) button = bell.AddComponent<HoverPressButton>();
        HoverOutlineSetup.Configure(bell);
        foreach (var t in bell.GetComponentsInChildren<Transform>())
            if (t.name == "Button") { button.pressVisual = t; break; }
        bool soundAssigned = false;
        foreach (var action in button.onPressed)
        {
            if (action == null) continue;
            for (int i = 0; i < action.GetPersistentEventCount(); i++)
                if (action.GetPersistentTarget(i) == audio && action.GetPersistentMethodName(i) == nameof(AudioSource.Play)) soundAssigned = true;
        }
        if (!soundAssigned)
        {
            if (button.onPressed.Count == 0) button.onPressed.Add(new UnityEvent());
            if (button.onPressed[0] == null) button.onPressed[0] = new UnityEvent();
            UnityEventTools.AddPersistentListener(button.onPressed[0], audio.Play);
        }
        EditorUtility.SetDirty(audio);
        EditorUtility.SetDirty(interactable);
        EditorUtility.SetDirty(button);
    }
}
