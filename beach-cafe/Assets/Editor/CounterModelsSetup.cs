using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CounterModelsSetup
{
    private const string Root = "Assets/BeachRestaurant/Models/";

    [MenuItem("Beach Restaurant/Setup Soda Cup and Bell Models")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        ConfigureCup();
        ConfigureBell();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureCup()
    {
        var cup = GameObject.Find("Cup");
        if (!cup) throw new System.InvalidOperationException("Scene Cup is missing.");
        var old = cup.transform.Find("Soda Cup Model");
        if (old) Object.DestroyImmediate(old.gameObject);
        if (cup.GetComponent<MeshRenderer>()) cup.GetComponent<MeshRenderer>().enabled = false;
        foreach (var collider in cup.GetComponents<Collider>()) Object.DestroyImmediate(collider);
        cup.transform.localScale = Vector3.one;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SodaCup/SodaCup.fbx"));
        model.name = "Soda Cup Model";
        model.transform.SetParent(cup.transform, false);
        var renderer = model.GetComponentInChildren<MeshRenderer>();
        model.transform.localScale *= 0.28f / renderer.bounds.size.y;
        var bounds = renderer.bounds;
        model.transform.position += new Vector3(cup.transform.position.x - bounds.center.x, cup.transform.position.y - bounds.min.y, cup.transform.position.z - bounds.center.z);
        var materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            string name = materials[i].name;
            Color color = name.Contains("001") ? new Color(0f, 0.59f, 1f) : name.Contains("004") ? new Color(1f, 0f, 0f) : Color.white;
            materials[i] = Material("SodaCup/" + name + ".mat", color, 0f, 0.3f);
        }
        renderer.sharedMaterials = materials;
        var mesh = model.GetComponentInChildren<MeshFilter>();
        var shape = mesh.gameObject.AddComponent<MeshCollider>();
        shape.sharedMesh = mesh.sharedMesh;
        shape.convex = true;
        var grab = cup.GetComponent<XRGrabInteractable>();
        grab.colliders.Clear();
        grab.colliders.Add(shape);
        var serving = cup.GetComponent<ServingItem>() ?? cup.AddComponent<ServingItem>();
        serving.itemType = OrderItem.Soda;
        SavePrefab(cup, Root + "SodaCup/Soda Cup.prefab");
    }

    private static void ConfigureBell()
    {
        var existing = GameObject.Find("Table Bell");
        if (existing)
        {
            BellSceneSetup.Configure(existing);
            SavePrefab(existing, Root + "TableBell/Table Bell.prefab");
            return;
        }
        var root = new GameObject("Table Bell");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "TableBell/TableBell.fbx"));
        model.transform.SetParent(root.transform, false);
        var bounds = Bounds(root);
        model.transform.localScale *= 0.16f / Mathf.Max(bounds.size.x, bounds.size.z);
        bounds = Bounds(root);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i].name.Contains("Black") ? "Black_metal" : "Metal";
                var mat = Material("TableBell/" + name + ".mat", Color.white, 1f, name == "Metal" ? 0.7f : 0.3f);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "TableBell/" + name + "_Base_Color.png"));
                mat.SetTexture("_OcclusionMap", LinearTexture("TableBell/" + name + "_Mixed_AO.png"));
                mat.SetFloat("_OcclusionStrength", 0.7f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTexture("_MetallicGlossMap", LinearTexture("TableBell/" + name + "_Metallic.png"));
                mat.SetFloat("_GlossMapScale", name == "Metal" ? 0.7f : 0.3f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(mat);
                materials[i] = mat;
            }
            renderer.sharedMaterials = materials;
            var mesh = renderer.GetComponent<MeshFilter>();
            if (mesh) renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        root.transform.SetParent(GameObject.Find("Workstations").transform, false);
        root.transform.position = new Vector3(0.12f, 1.0205f, 0.98f);
        BellSceneSetup.Configure(root);
        SavePrefab(root, Root + "TableBell/Table Bell.prefab");
    }

    private static Texture2D LinearTexture(string path)
    {
        path = Root + path;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer && importer.sRGBTexture) { importer.sRGBTexture = false; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material Material(string path, Color color, float metallic, float smoothness)
    {
        path = Root + path;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Bounds Bounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    public static void SavePrefab(GameObject root, string path)
    {
        Vector3 position = root.transform.position;
        Quaternion rotation = root.transform.rotation;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        root.transform.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
    }
}
