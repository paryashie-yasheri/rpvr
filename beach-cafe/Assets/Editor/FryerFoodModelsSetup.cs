using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FryerFoodModelsSetup
{
    private const string Root = "Assets/BeachRestaurant/Models/Fries/";

    [MenuItem("Beach Restaurant/Import Fries Models")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var loose = BuildVisual("Scopia/frenchFries.obj", "Scopia/textureFrenchFriesRaw.png", "Loose Fries", 0.23f, Quaternion.identity);
        var boxed = BuildVisual("Lowpoly/Fries.obj", "Lowpoly/teks.png", "Boxed Fries", 0.18f, Quaternion.Euler(0, 90, 0));
        PrefabUtility.SaveAsPrefabAsset(loose, Root + "Loose Fries.prefab");
        PrefabUtility.SaveAsPrefabAsset(boxed, Root + "Boxed Fries.prefab");
        var fries = GameObject.Find("Fries");
        if (!fries) throw new System.InvalidOperationException("Fries scene object is missing.");
        if (!fries.GetComponent<CookingProgressBar>()) fries.AddComponent<CookingProgressBar>();
        for (int i = fries.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(fries.transform.GetChild(i).gameObject);
        loose.transform.SetParent(fries.transform, false);
        loose.name = "Fries Model";
        var bounds = GetBounds(loose);
        var collider = fries.GetComponent<BoxCollider>();
        collider.center = fries.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
        // Keep the cookable/grab/Rigidbody on the existing scene root.
        // The boxed serving model is a separate visual prefab for serving logic.
        Object.DestroyImmediate(boxed);
        var gradient = fries.GetComponent<FryerCookableProgressGradient>();
        gradient.gradient.SetKeys(new[] {
            new GradientColorKey(Color.white, 0),
            new GradientColorKey(new Color(1f, 0.92f, 0.72f), 0.4f / 1.5f),
            new GradientColorKey(new Color(1f, 0.72f, 0.24f), 0.8f / 1.5f),
            new GradientColorKey(new Color(0.72f, 0.36f, 0.10f), 1.2f / 1.5f),
            new GradientColorKey(new Color(0.07f, 0.045f, 0.025f), 1)
        }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        EditorUtility.SetDirty(gradient);
        EditorSceneManager.MarkSceneDirty(fries.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = fries;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static GameObject BuildVisual(string modelPath, string texturePath, string name, float width, Quaternion rotation)
    {
        var root = new GameObject(name);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + modelPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = rotation;
        var bounds = GetBounds(root);
        model.transform.localScale *= width / bounds.size.x;
        bounds = GetBounds(root);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var materialPath = Root + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.16f);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
        return root;
    }

    private static Bounds GetBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
