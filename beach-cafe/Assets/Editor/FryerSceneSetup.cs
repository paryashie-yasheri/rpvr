using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class FryerSceneSetup
{
    [MenuItem("Beach Restaurant/Setup Fryer")]
    public static void Setup()
    {
        const string scenePath = "Assets/BeachRestaurant/Scenes/BeachRestaurant_Blockout.unity";
        if (EditorSceneManager.GetActiveScene().path != scenePath)
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var oil = GameObject.Find("Fryer Oil Placeholder");
        var workstations = GameObject.Find("Workstations");
        if (!oil || !workstations)
        {
            Debug.LogError("Open the Beach Restaurant blockout scene before setting up the fryer.");
            return;
        }

        ConfigureOil(oil);
        CreateOpenBasket();
        CreateFries(workstations.transform);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Fryer setup saved: open basket, hot-oil trigger, bubbles, ready tone, and a grabbable fries portion.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void ConfigureOil(GameObject oil)
    {
        var zone = oil.GetComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(1f, 7f, 1f);
        zone.center = new Vector3(0f, 2.5f, 0f);
        if (!oil.GetComponent<Fryer>()) Undo.AddComponent<Fryer>(oil);
        EditorUtility.SetDirty(zone);
        EditorUtility.SetDirty(oil);
    }

    private static void CreateOpenBasket()
    {
        var basket = GameObject.Find("Fryer Basket");
        if (!basket) return;

        if (basket.transform.Find("Basket Floor")) return;
        Undo.RecordObject(basket.transform, "Open fryer basket");
        basket.transform.localScale = Vector3.one;
        var renderer = basket.GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = false;
        var collider = basket.GetComponent<BoxCollider>();
        if (collider) collider.enabled = false;

        var darkMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachRestaurant/Materials/BR_DarkMetal.mat");
        CreateBar("Basket Floor", basket.transform, new Vector3(0f, -0.045f, 0f), new Vector3(0.30f, 0.012f, 0.28f), darkMetal, true);

        const float halfX = 0.15f;
        const float halfZ = 0.14f;
        const float low = -0.04f;
        const float high = 0.05f;
        const float rod = 0.009f;
        CreateBar("Basket Rim Left", basket.transform, new Vector3(-halfX, high, 0f), new Vector3(rod, rod, halfZ * 2f), darkMetal);
        CreateBar("Basket Rim Right", basket.transform, new Vector3(halfX, high, 0f), new Vector3(rod, rod, halfZ * 2f), darkMetal);
        CreateBar("Basket Rim Front", basket.transform, new Vector3(0f, high, -halfZ), new Vector3(halfX * 2f, rod, rod), darkMetal);
        CreateBar("Basket Rim Back", basket.transform, new Vector3(0f, high, halfZ), new Vector3(halfX * 2f, rod, rod), darkMetal);

        for (int i = -2; i <= 2; i++)
        {
            float x = i * 0.05f;
            CreateBar("Basket Wire", basket.transform, new Vector3(x, (low + high) * 0.5f, -halfZ), new Vector3(rod, high - low, rod), darkMetal);
            CreateBar("Basket Wire", basket.transform, new Vector3(x, (low + high) * 0.5f, halfZ), new Vector3(rod, high - low, rod), darkMetal);
        }
        for (int i = -1; i <= 1; i++)
        {
            float z = i * 0.065f;
            CreateBar("Basket Wire", basket.transform, new Vector3(-halfX, (low + high) * 0.5f, z), new Vector3(rod, high - low, rod), darkMetal);
            CreateBar("Basket Wire", basket.transform, new Vector3(halfX, (low + high) * 0.5f, z), new Vector3(rod, high - low, rod), darkMetal);
        }
        for (int i = -2; i <= 2; i++)
        {
            float x = i * 0.05f;
            CreateBar("Basket Bottom Wire", basket.transform, new Vector3(x, low, 0f), new Vector3(rod, rod, halfZ * 1.8f), darkMetal);
        }

        var handle = GameObject.Find("Fryer Basket Handle");
        if (handle && handle.transform.parent != basket.transform)
            handle.transform.SetParent(basket.transform, true);
        EditorUtility.SetDirty(basket);
    }

    private static void CreateBar(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool withCollider = false)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = objectName;
        Undo.RegisterCreatedObjectUndo(bar, "Build fryer basket");
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = localScale;
        bar.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!withCollider) Object.DestroyImmediate(bar.GetComponent<BoxCollider>());
    }

    private static void CreateFries(Transform parent)
    {
        if (GameObject.Find("Fries")) return;

        var fries = new GameObject("Fries");
        Undo.RegisterCreatedObjectUndo(fries, "Add fries portion");
        fries.transform.SetParent(parent, false);
        fries.transform.SetPositionAndRotation(new Vector3(-0.66f, 1.15f, 0.38f), Quaternion.identity);
        var body = fries.AddComponent<Rigidbody>();
        body.mass = 0.16f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var collider = fries.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.21f, 0.18f, 0.19f);
        collider.center = new Vector3(0f, 0.075f, 0f);

        var cookable = fries.AddComponent<FryerCookable>();
        cookable.secondsToFry = 30f;
        fries.AddComponent<CookingProgressBar>();
        var gradient = fries.AddComponent<FryerCookableProgressGradient>();
        gradient.progressRange = 1.5f;

        var grab = fries.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.colliders.Clear();
        grab.colliders.Add(collider);

        var potatoMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachRestaurant/Materials/BR_FryerZone.mat");
        var sticks = new[]
        {
            new Vector3(-0.055f, 0.075f, -0.035f),
            new Vector3(-0.028f, 0.080f, 0.028f),
            new Vector3(0f, 0.082f, -0.01f),
            new Vector3(0.03f, 0.078f, 0.04f),
            new Vector3(0.055f, 0.08f, -0.04f),
            new Vector3(0.01f, 0.085f, 0.07f)
        };
        for (int i = 0; i < sticks.Length; i++)
        {
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stick.name = "Potato Stick " + (i + 1);
            Undo.RegisterCreatedObjectUndo(stick, "Create fries mesh");
            stick.transform.SetParent(fries.transform, false);
            stick.transform.localPosition = sticks[i];
            stick.transform.localRotation = Quaternion.Euler(0f, (i * 17f) - 35f, (i % 2 == 0 ? -1f : 1f) * 5f);
            stick.transform.localScale = new Vector3(0.032f, 0.15f, 0.032f);
            stick.GetComponent<MeshRenderer>().sharedMaterial = potatoMaterial;
            Object.DestroyImmediate(stick.GetComponent<BoxCollider>());
        }

        EditorUtility.SetDirty(fries);
    }
}
