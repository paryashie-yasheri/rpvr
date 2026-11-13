using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class TraySceneSetup
{
    [MenuItem("Beach Restaurant/Setup Tray Slots")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var tray = GameObject.Find("Serving Tray");
        if (!tray) throw new System.InvalidOperationException("Serving Tray is missing.");
        var preview = MakeMaterial("BR_TraySnapPreview", new Color(1, 1, 1, 0.35f));
        var outline = MakeMaterial("BR_TraySlotOutline", new Color(0.2f, 0.34f, 0.36f, 0.5f));
        var burger = MakeSlot(tray, "Burger Slot", OrderItem.Burger, new Vector2(-0.135f, 0), new Vector2(0.25f, 0.28f), preview, outline);
        var fries = MakeSlot(tray, "Fries Slot", OrderItem.Fries, new Vector2(0.125f, -0.09f), new Vector2(0.25f, 0.25f), preview, outline);
        var soda = MakeSlot(tray, "Soda Slot", OrderItem.Soda, new Vector2(0.125f, 0.125f), new Vector2(0.17f, 0.17f), preview, outline);
        var contents = tray.GetComponent<TrayContents>() ?? tray.AddComponent<TrayContents>();
        contents.slots = new[] { burger, fries, soda };
        var portion = GameObject.Find("Fries");
        var item = portion.GetComponent<ServingItem>() ?? portion.AddComponent<ServingItem>();
        item.itemType = OrderItem.Fries;
        var cup = GameObject.Find("Cup");
        var drink = cup.GetComponent<ServingItem>();
        if (!drink) drink = cup.AddComponent<ServingItem>();
        drink.itemType = OrderItem.Soda;
        EditorUtility.SetDirty(drink);
        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(contents);
        CounterModelsSetup.SavePrefab(tray, "Assets/BeachRestaurant/Models/Tray/Serving Tray.prefab");
        EditorSceneManager.MarkSceneDirty(tray.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static TraySlot MakeSlot(GameObject tray, string name, OrderItem kind, Vector2 position, Vector2 size, Material preview, Material outline)
    {
        var existing = tray.transform.Find(name);
        if (existing) Object.DestroyImmediate(existing.gameObject);
        var root = new GameObject(name);
        root.transform.SetParent(tray.transform, false);
        Vector3 world = tray.transform.TransformPoint(new Vector3(position.x, 0, position.y));
        var trayCollider = tray.GetComponentInChildren<MeshCollider>();
        if (trayCollider.Raycast(new Ray(world + Vector3.up, Vector3.down), out var hit, 2f)) world.y = hit.point.y + 0.0015f;
        root.transform.position = world;
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        float height = kind == OrderItem.Soda ? 0.22f : 0.18f;
        trigger.center = new Vector3(0, height * 0.5f - 0.005f, 0);
        trigger.size = new Vector3(size.x, height, size.y);
        var slot = root.AddComponent<TraySlot>();
        slot.acceptedItem = kind;
        slot.showInteractableHoverMeshes = true;
        slot.interactableHoverMeshMaterial = preview;
        slot.interactableCantHoverMeshMaterial = preview;
        slot.hoverSocketSnapping = false;
        slot.recycleDelayTime = 0.75f;
        var anchor = new GameObject("Placement Surface").transform;
        anchor.SetParent(root.transform, false);
        slot.attachTransform = anchor;
        var hint = new GameObject("Empty Slot Hint");
        hint.transform.SetParent(root.transform, false);
        var line = hint.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.sharedMaterial = outline;
        line.widthMultiplier = 0.0015f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = kind != OrderItem.Fries ? 40 : 4;
        for (int i = 0; i < line.positionCount; i++)
        {
            if (kind != OrderItem.Fries)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                float radius = kind == OrderItem.Soda ? 0.065f : 0.1f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.001f, Mathf.Sin(angle) * radius));
            }
            else line.SetPosition(i, new Vector3(i == 0 || i == 3 ? -0.115f : 0.115f, 0.001f, i < 2 ? -0.115f : 0.115f));
        }
        slot.emptyHint = hint;
        return slot;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = "Assets/BeachRestaurant/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }
}
