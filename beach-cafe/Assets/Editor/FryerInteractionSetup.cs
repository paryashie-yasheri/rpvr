using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

public static class FryerInteractionSetup
{
    [MenuItem("Beach Restaurant/Setup Fryer Oil and Retrieval")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        const string path = "Assets/BeachRestaurant/Scenes/BeachRestaurant_Blockout.unity";
        if (EditorSceneManager.GetActiveScene().path != path) EditorSceneManager.OpenScene(path);
        Configure();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static void Configure()
    {
        var oil = GameObject.Find("Fryer Cooking Zone") ?? GameObject.Find("Fryer Oil Placeholder");
        var basket = GameObject.Find("Fryer Basket");
        var vat = GameObject.Find("Fryer Vat");
        if (!oil || !basket || !vat) throw new System.InvalidOperationException("Fryer scene objects are missing.");

        oil.name = "Fryer Cooking Zone";
        oil.transform.position = new Vector3(-1.9f, 1.145f, -0.05f);
        oil.transform.localScale = Vector3.one;
        oil.GetComponent<MeshRenderer>().enabled = false;
        var zone = oil.GetComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = Vector3.zero;
        zone.size = new Vector3(0.42f, 0.09f, 0.38f);

        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachRestaurant/Materials/BR_DarkMetal.mat");
        vat.GetComponent<MeshRenderer>().enabled = false;
        vat.GetComponent<BoxCollider>().enabled = false;
        vat.transform.localScale = Vector3.one;
        CreatePart(vat.transform, "Vat Floor", new Vector3(0, -0.08f, 0), new Vector3(0.52f, 0.025f, 0.52f), metal, true);
        CreatePart(vat.transform, "Vat Left", new Vector3(-0.245f, 0.09f, 0), new Vector3(0.03f, 0.32f, 0.52f), metal, true);
        CreatePart(vat.transform, "Vat Right", new Vector3(0.245f, 0.09f, 0), new Vector3(0.03f, 0.32f, 0.52f), metal, true);
        CreatePart(vat.transform, "Vat Front", new Vector3(0, 0.09f, -0.245f), new Vector3(0.46f, 0.32f, 0.03f), metal, true);
        CreatePart(vat.transform, "Vat Back", new Vector3(0, 0.09f, 0.245f), new Vector3(0.46f, 0.32f, 0.03f), metal, true);

        const string materialPath = "Assets/BeachRestaurant/Materials/BR_LiquidOil.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetColor("_BaseColor", new Color(0.66f, 0.33f, 0.035f, 0.72f));
        material.SetFloat("_Smoothness", 0.9f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Surface", 1f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        var surface = CreatePart(oil.transform, "Liquid Oil Surface", new Vector3(0, 0.035f, 0), new Vector3(0.45f, 0.004f, 0.43f), material, false);
        surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        basket.transform.position = new Vector3(-1.9f, 1.16f, -0.05f);
        var floor = basket.transform.Find("Basket Floor").GetComponent<MeshRenderer>();
        floor.enabled = false; // Retain the submerged collision floor; show the wire mesh.
        var handle = basket.transform.Find("Fryer Basket Handle");
        handle.localPosition = new Vector3(0, 0.11f, -0.30f);
        var collider = basket.GetComponent<BoxCollider>();
        collider.enabled = true;
        collider.isTrigger = true;
        collider.center = new Vector3(0, 0.01f, 0);
        collider.size = new Vector3(0.34f, 0.12f, 0.32f);
        var interaction = basket.GetComponent<FryerBasket>() ?? basket.AddComponent<FryerBasket>();
        interaction.fryer = oil.GetComponent<Fryer>();
        var target = basket.GetComponent<XRSimpleInteractable>() ?? basket.AddComponent<XRSimpleInteractable>();
        var serialized = new SerializedObject(target);
        var colliders = serialized.FindProperty("m_Colliders");
        colliders.arraySize = 2;
        colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
        colliders.GetArrayElementAtIndex(1).objectReferenceValue = handle.GetComponent<BoxCollider>();
        serialized.FindProperty("m_InteractionLayers.m_Bits").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        interaction.highlightRenderers = basket.GetComponentsInChildren<Renderer>();
        // The basket's broad hover volume is a trigger so food can fall through it.
        // Let both hands' near casters detect it; far rays can still hit the handle.
        foreach (var hand in Object.FindObjectsByType<NearFarInteractor>())
        {
            var caster = hand.GetComponent<SphereInteractionCaster>();
            if (!caster) continue;
            caster.physicsTriggerInteraction = QueryTriggerInteraction.Collide;
            EditorUtility.SetDirty(caster);
            PrefabUtility.RecordPrefabInstancePropertyModifications(caster);
        }
        EditorUtility.SetDirty(interaction);
        EditorSceneManager.MarkSceneDirty(oil.scene);
    }

    private static GameObject CreatePart(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid)
    {
        var existing = parent.Find(name);
        var part = existing ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!solid && part.GetComponent<Collider>()) Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }
}
