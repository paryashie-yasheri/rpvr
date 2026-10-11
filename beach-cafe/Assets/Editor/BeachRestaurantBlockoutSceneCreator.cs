using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeachRestaurantBlockoutSceneCreator
{
    private const string MenuPath = "Beach Restaurant/Create Blockout Scene";
    private const string MaterialFolder = "Assets/BeachRestaurant/Materials";
    private const string SceneFolder = "Assets/BeachRestaurant/Scenes";
    private const string SceneBaseName = "BeachRestaurant_Blockout";

    private const float SandSize = 12f;
    private const float SandThickness = 0.08f;
    private const float CounterHeight = 0.9f;
    private const float CounterDepth = 0.6f;
    private const float CounterThickness = 0.12f;
    private const float CounterTopY = CounterHeight + CounterThickness * 0.5f;
    private const float KioskWidth = 3.4f;
    private const float KioskDepth = 2.8f;
    private const float CanopyHeight = 2.35f;
    private const float SideCounterX = 1.25f;
    private const float SideCounterZ = -0.35f;
    private const float SideCounterLength = 1.65f;
    private const float FrontCounterZ = 0.62f;
    private const float FrontCounterWidth = 2.5f;

    private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -0.52f);
    private static readonly Quaternion PlayerRotation = Quaternion.identity;

    [MenuItem(MenuPath)]
    public static void CreateBlockoutScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(SceneFolder);
        AssetDatabase.Refresh();

        var materials = CreateOrUpdateMaterials();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = SceneBaseName;

        var environment = new GameObject("Environment");
        var kiosk = new GameObject("Kiosk");
        var workstations = new GameObject("Workstations");
        var props = new GameObject("Props");
        var labels = new GameObject("Labels");
        var playerStart = new GameObject("PlayerStart");
        playerStart.transform.SetPositionAndRotation(PlayerPosition, PlayerRotation);

        CreateEnvironment(environment.transform, materials);
        CreateKiosk(kiosk.transform, materials);
        CreateWorkstations(workstations.transform, materials);
        CreateProps(props.transform, materials);
        CreateLabels(labels.transform, materials);
        CreateLightingAndCamera(playerStart.transform);

        var path = AssetDatabase.GenerateUniqueAssetPath($"{SceneFolder}/{SceneBaseName}.unity");
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorUtility.DisplayDialog("Beach Restaurant", $"Blockout scene created:\n{path}", "OK");
    }

    private static Dictionary<string, Material> CreateOrUpdateMaterials()
    {
        return new Dictionary<string, Material>
        {
            { "Sand", SaveMaterial("BR_Sand.mat", new Color(0.82f, 0.70f, 0.46f)) },
            { "Wood", SaveMaterial("BR_Wood.mat", new Color(0.54f, 0.34f, 0.16f)) },
            { "DarkWood", SaveMaterial("BR_DarkWood.mat", new Color(0.33f, 0.20f, 0.10f)) },
            { "Metal", SaveMaterial("BR_Metal.mat", new Color(0.58f, 0.62f, 0.63f)) },
            { "DarkMetal", SaveMaterial("BR_DarkMetal.mat", new Color(0.12f, 0.13f, 0.13f)) },
            { "Assembly", SaveMaterial("BR_AssemblyZone.mat", new Color(0.28f, 0.62f, 0.44f)) },
            { "Grill", SaveMaterial("BR_GrillZone.mat", new Color(0.18f, 0.18f, 0.16f)) },
            { "Fryer", SaveMaterial("BR_FryerZone.mat", new Color(0.88f, 0.73f, 0.23f)) },
            { "Drink", SaveMaterial("BR_DrinkZone.mat", new Color(0.20f, 0.45f, 0.78f)) },
            { "Tray", SaveMaterial("BR_Tray.mat", new Color(0.82f, 0.82f, 0.76f)) },
            { "Order", SaveMaterial("BR_OrderCard.mat", new Color(0.96f, 0.94f, 0.78f)) },
            { "Patty", SaveMaterial("BR_Patty.mat", new Color(0.28f, 0.12f, 0.07f)) },
            { "Bun", SaveMaterial("BR_Bun.mat", new Color(0.93f, 0.63f, 0.28f)) },
            { "Cup", SaveMaterial("BR_Cup.mat", new Color(0.92f, 0.24f, 0.20f)) },
            { "White", SaveMaterial("BR_White.mat", new Color(0.95f, 0.95f, 0.92f)) }
        };
    }

    private static Material SaveMaterial(string fileName, Color color)
    {
        var path = $"{MaterialFolder}/{fileName}";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindCompatibleShader());
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = FindCompatibleShader();
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindCompatibleShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
    }

    private static void CreateEnvironment(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCube("Sand Platform 12x12m", parent, Vector3.zero, new Vector3(SandSize, SandThickness, SandSize), materials["Sand"]);
    }

    private static void CreateKiosk(Transform parent, Dictionary<string, Material> materials)
    {
        var postPositions = new[]
        {
            new Vector3(-KioskWidth * 0.5f, 1.25f, -KioskDepth * 0.5f),
            new Vector3(KioskWidth * 0.5f, 1.25f, -KioskDepth * 0.5f),
            new Vector3(-KioskWidth * 0.5f, 1.25f, KioskDepth * 0.5f),
            new Vector3(KioskWidth * 0.5f, 1.25f, KioskDepth * 0.5f)
        };

        foreach (var position in postPositions)
        {
            CreateCube("Wood Post", parent, position, new Vector3(0.16f, 2.25f, 0.16f), materials["Wood"]);
        }

        CreateCube("Simple Canopy", parent, new Vector3(0f, CanopyHeight, 0f), new Vector3(KioskWidth + 0.35f, 0.14f, KioskDepth + 0.35f), materials["DarkWood"]);
        CreateCube("Back Header Beam", parent, new Vector3(0f, 2.02f, KioskDepth * 0.5f), new Vector3(KioskWidth + 0.15f, 0.16f, 0.14f), materials["Wood"]);
        CreateCube("Front Header Beam", parent, new Vector3(0f, 2.02f, -KioskDepth * 0.5f), new Vector3(KioskWidth + 0.15f, 0.16f, 0.14f), materials["Wood"]);
    }

    private static void CreateWorkstations(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCounter(parent, "Front Counter", new Vector3(0f, CounterTopY, FrontCounterZ), new Vector3(FrontCounterWidth, CounterThickness, CounterDepth), Axis.Z, materials["Wood"]);
        CreateCounter(parent, "Left Counter", new Vector3(-SideCounterX, CounterTopY, SideCounterZ), new Vector3(CounterDepth, CounterThickness, SideCounterLength), Axis.X, materials["Wood"]);
        CreateCounter(parent, "Right Counter", new Vector3(SideCounterX, CounterTopY, SideCounterZ), new Vector3(CounterDepth, CounterThickness, SideCounterLength), Axis.X, materials["Wood"]);

        CreateCube("Assembly Zone", parent, new Vector3(0f, CounterHeight + 0.075f, FrontCounterZ - 0.18f), new Vector3(0.95f, 0.03f, 0.4f), materials["Assembly"]);

        CreateGrill(parent, materials);
        CreateFryer(parent, materials);
        CreateDrinkDispenser(parent, materials);
        CreateTrayAndBell(parent, materials);
    }

    private enum Axis
    {
        X,
        Z
    }

    private static void CreateCounter(Transform parent, string name, Vector3 position, Vector3 scale, Axis panelAxis, Material material)
    {
        CreateCube(name, parent, position, scale, material);

        var panelPosition = position + new Vector3(0f, -CounterHeight * 0.5f, 0f);
        var panelScale = panelAxis == Axis.Z
            ? new Vector3(scale.x, CounterHeight, 0.08f)
            : new Vector3(0.08f, CounterHeight, scale.z);

        CreateCube($"{name} Support Panel", parent, panelPosition, panelScale, material);
    }

    private static void CreateGrill(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCube("Grill Base", parent, new Vector3(-SideCounterX, 1.04f, -0.75f), new Vector3(0.46f, 0.18f, 0.5f), materials["Metal"]);
        CreateCube("Grill Cooking Surface", parent, new Vector3(-SideCounterX, 1.145f, -0.75f), new Vector3(0.4f, 0.04f, 0.42f), materials["Grill"]);
        CreateCube("Grill Back Lip", parent, new Vector3(-SideCounterX, 1.24f, -0.52f), new Vector3(0.44f, 0.13f, 0.04f), materials["DarkMetal"]);
    }

    private static void CreateFryer(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCube("Fryer Vat", parent, new Vector3(-SideCounterX, 1.03f, -0.1f), new Vector3(0.46f, 0.2f, 0.44f), materials["Metal"]);
        CreateCube("Fryer Oil Placeholder", parent, new Vector3(-SideCounterX, 1.155f, -0.1f), new Vector3(0.36f, 0.03f, 0.32f), materials["Fryer"]);
        CreateCube("Fryer Basket", parent, new Vector3(-SideCounterX, 1.23f, -0.1f), new Vector3(0.3f, 0.1f, 0.28f), materials["DarkMetal"]);
        CreateCube("Fryer Basket Handle", parent, new Vector3(-SideCounterX, 1.25f, -0.38f), new Vector3(0.08f, 0.06f, 0.3f), materials["DarkMetal"]);
    }

    private static void CreateDrinkDispenser(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCube("Drink Dispenser Body", parent, new Vector3(SideCounterX, 1.22f, -0.38f), new Vector3(0.44f, 0.58f, 0.42f), materials["Drink"]);
        CreateCube("Drink Dispenser Face", parent, new Vector3(SideCounterX, 1.14f, -0.62f), new Vector3(0.3f, 0.22f, 0.05f), materials["White"]);
        CreateCube("Drink Nozzle", parent, new Vector3(SideCounterX, 1.04f, -0.82f), new Vector3(0.1f, 0.08f, 0.18f), materials["DarkMetal"]);
    }

    private static void CreateTrayAndBell(Transform parent, Dictionary<string, Material> materials)
    {
        CreateCube("Serving Tray", parent, new Vector3(-0.35f, 1.005f, FrontCounterZ + 0.26f), new Vector3(0.5f, 0.04f, 0.32f), materials["Tray"]);
        var bell = CreateCylinder("Service Bell", parent, new Vector3(0.35f, 1.04f, FrontCounterZ + 0.26f), new Vector3(0.15f, 0.07f, 0.15f), materials["Metal"]);
        bell.transform.rotation = Quaternion.identity;
        CreateSphere("Bell Button", parent, new Vector3(0.35f, 1.1f, FrontCounterZ + 0.26f), new Vector3(0.07f, 0.035f, 0.07f), materials["DarkMetal"]);
    }

    private static void CreateProps(Transform parent, Dictionary<string, Material> materials)
    {
        CreatePropCylinder("Patty", parent, new Vector3(-0.22f, 1.08f, 0.35f), new Vector3(0.2f, 0.07f, 0.2f), materials["Patty"], 0.25f);
        CreatePropCylinder("Bottom Bun", parent, new Vector3(0.1f, 1.07f, 0.35f), new Vector3(0.24f, 0.06f, 0.24f), materials["Bun"], 0.2f);
        CreatePropSphere("Top Bun", parent, new Vector3(0.42f, 1.09f, 0.35f), new Vector3(0.24f, 0.1f, 0.24f), materials["Bun"], 0.18f);
        CreatePropCylinder("Cup", parent, new Vector3(0.95f, 1.25f, -0.78f), new Vector3(0.14f, 0.24f, 0.14f), materials["Cup"], 0.12f);
    }

    private static void CreateLabels(Transform parent, Dictionary<string, Material> materials)
    {
        var card = CreateCube("Fixed Order Card", parent, new Vector3(0f, 1.75f, 0.78f), new Vector3(1.25f, 0.55f, 0.04f), materials["Order"]);
        card.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private static void CreateLightingAndCamera(Transform playerStart)
    {
        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        var cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetPositionAndRotation(PlayerPosition + new Vector3(0f, 1.7f, 0f), PlayerRotation);
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 50f;
        cameraObject.transform.SetParent(playerStart, true);
        Camera.main?.gameObject.SetActive(false);
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        ApplyMaterial(gameObject, material);
        return gameObject;
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        ApplyMaterial(gameObject, material);
        return gameObject;
    }

    private static GameObject CreateSphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        ApplyMaterial(gameObject, material);
        return gameObject;
    }

    private static void CreatePropCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material, float mass)
    {
        var prop = CreateCylinder(name, parent, position, scale, material);
        var body = prop.AddComponent<Rigidbody>();
        body.mass = mass;
    }

    private static void CreatePropSphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material, float mass)
    {
        var prop = CreateSphere(name, parent, position, scale, material);
        var body = prop.AddComponent<Rigidbody>();
        body.mass = mass;
    }

    private static void CreateLabel(Transform parent, string text, Vector3 position, Quaternion rotation, float characterSize)
    {
        var labelObject = new GameObject(text.Replace('\n', ' '));
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = position;
        labelObject.transform.localRotation = rotation;

        var label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = characterSize;
        label.fontSize = 64;
        label.color = Color.black;
    }

    private static void ApplyMaterial(GameObject gameObject, Material material)
    {
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
