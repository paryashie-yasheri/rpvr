using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Arranges the café stations around a stationary cook.</summary>
public static class CircularCookWorkstationSetup
{
    private const string ScenePath = "Assets/BeachRestaurant/Scenes/BeachRestaurant_Blockout.unity";
    private const float CenterZ = -0.45f;
    private const float RingRadius = 0.58f;
    private const float CounterDepth = 0.48f;
    private const float CounterTopY = 0.96f;

    [MenuItem("RPVR/Setup Circular Cook Workstation")]
    public static void Apply()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before arranging the workstation.");

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open the main BeachRestaurant_Blockout scene first.");

        var workstations = GameObject.Find("Workstations").transform;
        var frontCounter = GameObject.Find("Front Counter");
        var existingRingSegment = GameObject.Find("Cook Ring Counter 1");
        var xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        var playerStart = GameObject.Find("PlayerStart");
        var assembly = GameObject.Find("Assembly Zone");
        var tray = GameObject.Find("Serving Tray");
        var bell = GameObject.Find("Service Bell");
        var bellButton = GameObject.Find("Bell Button");
        var orderCard = GameObject.Find("Fixed Order Card");
        var orderLabel = UnityEngine.Object.FindAnyObjectByType<OrderLabel>();
        var bottomBun = GameObject.Find("Bottom Bun");
        var topBun = GameObject.Find("Top Bun");
        var patty = GameObject.Find("Patty");
        var cup = GameObject.Find("Cup");
        var fries = GameObject.Find("Fries");
        var dispenserBody = GameObject.Find("Drink Dispenser Body");
        var fryerZone = GameObject.Find("Fryer Cooking Zone");
        var grillSurface = GameObject.Find("Grill Cooking Surface");

        Require(workstations && xrOrigin && playerStart && assembly && tray && bell && bellButton
                && orderCard && orderLabel && bottomBun && topBun && patty && cup && fries && dispenserBody && fryerZone && grillSurface,
            "The scene is missing one or more workstation objects.");
        Require(frontCounter || existingRingSegment, "The scene has no counter material source.");

        Undo.RegisterFullObjectHierarchyUndo(workstations.gameObject, "Arrange circular cook workstation");
        Undo.RecordObject(xrOrigin.transform, "Center player in cook workstation");
        Undo.RecordObject(playerStart.transform, "Center player start in cook workstation");

        Material counterMaterial = frontCounter
            ? frontCounter.GetComponent<Renderer>().sharedMaterial
            : existingRingSegment.GetComponent<Renderer>().sharedMaterial;
        RemoveOldCounters();
        RemoveRingGeometry();
        CreateRingGeometry(workstations, counterMaterial);

        MoveStation(workstations, "Grill Station", new[] { "Grill Base", "Grill Cooking Surface", "Grill Back Lip" },
            new Vector3(-RingRadius, 1.15f, CenterZ), -90f, new Vector3(-1.90f, 1.15f, -0.85f));
        MoveStation(workstations, "Fryer Station", new[] { "Fryer Vat", "Fryer Cooking Zone", "Fryer Basket" },
            new Vector3(RingRadius, 1.15f, CenterZ), 90f, new Vector3(-1.90f, 1.15f, -0.05f));
        MoveStation(workstations, "Drinks Station", new[] { "Drink Dispenser Body", "Drink Dispenser Face", "Drink Nozzle" },
            new Vector3(0f, 1.34f, CenterZ - RingRadius), 180f, new Vector3(1.90f, 1.25f, -0.45f));

        var nozzle = GameObject.Find("Drink Nozzle").transform;
        Undo.RecordObject(nozzle, "Aim drink nozzle into the workstation");
        nozzle.localPosition = new Vector3(nozzle.localPosition.x, nozzle.localPosition.y, -0.32f);

        Move(xrOrigin.transform, new Vector3(0f, 0f, CenterZ), Quaternion.identity);
        Move(playerStart.transform, new Vector3(0f, 0f, CenterZ), Quaternion.identity);
        Move(assembly.transform, new Vector3(-0.12f, 0.98f, CenterZ + 0.54f), Quaternion.identity);
        assembly.transform.localScale = new Vector3(0.90f, 0.03f, 0.36f);
        Move(bottomBun.transform, new Vector3(-0.12f, 1.07f, CenterZ + 0.54f), Quaternion.identity);
        Move(patty.transform, new Vector3(-0.45f, 1.08f, CenterZ + 0.54f), Quaternion.identity);
        Move(topBun.transform, new Vector3(0.22f, 1.09f, CenterZ + 0.54f), Quaternion.identity);
        Move(tray.transform, new Vector3(0.33f, 1.01f, CenterZ + 0.54f), Quaternion.identity);
        tray.transform.localScale = new Vector3(0.46f, 0.04f, 0.28f);
        Move(bell.transform, new Vector3(0.48f, 1.04f, CenterZ + 0.40f), Quaternion.identity);
        Move(bellButton.transform, new Vector3(0.48f, 1.10f, CenterZ + 0.40f), Quaternion.identity);
        Move(orderCard.transform, new Vector3(0f, 1.75f, CenterZ + 0.77f), Quaternion.Euler(0f, 180f, 0f));
        Move(orderLabel.transform, new Vector3(0f, 1.79f, CenterZ + 0.72f), Quaternion.Euler(0f, 180f, 0f));
        Move(cup.transform, new Vector3(0.12f, 1.30f, CenterZ - 0.35f), Quaternion.identity);
        Move(fries.transform, new Vector3(-0.45f, 1.15f, CenterZ + 0.28f), Quaternion.identity);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Circular cook workstation saved: grill left, fryer right, drinks behind, assembly and handoff in front.");
    }

    private static void CreateRingGeometry(Transform parent, Material material)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 surfacePosition = new Vector3(direction.x * RingRadius, CounterTopY, CenterZ + direction.z * RingRadius);
            float width = i == 0 ? 1.18f : 0.76f;
            CreateCube(parent, $"Cook Ring Counter {i + 1}", surfacePosition, new Vector3(width, 0.12f, CounterDepth), angle, material);

            Vector3 supportPosition = new Vector3(direction.x * (RingRadius + CounterDepth * 0.5f - 0.035f), 0.47f,
                CenterZ + direction.z * (RingRadius + CounterDepth * 0.5f - 0.035f));
            CreateCube(parent, $"Cook Ring Outer Support {i + 1}", supportPosition, new Vector3(width, 0.86f, 0.07f), angle, material);
        }
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 size, float yaw, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cube, "Create circular workstation counter");
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static void MoveStation(Transform parent, string groupName, string[] objectNames, Vector3 target, float yaw, Vector3 origin)
    {
        var group = parent.Find(groupName);
        if (!group)
        {
            var groupObject = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupObject, "Group workstation equipment");
            group = groupObject.transform;
            group.SetParent(parent, false);
            group.position = origin;
            group.rotation = Quaternion.identity;
            foreach (string objectName in objectNames)
            {
                var item = GameObject.Find(objectName);
                if (!item) throw new InvalidOperationException("Missing station object: " + objectName);
                Undo.SetTransformParent(item.transform, group, "Group workstation equipment");
            }
        }

        Undo.RecordObject(group, "Position workstation equipment");
        group.SetPositionAndRotation(origin, Quaternion.identity);
        group.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
    }

    private static void RemoveOldCounters()
    {
        foreach (string name in new[]
        {
            "Front Counter", "Front Counter Support Panel", "Left Counter", "Left Counter Support Panel",
            "Right Counter", "Right Counter Support Panel"
        })
        {
            var item = GameObject.Find(name);
            if (item) Undo.DestroyObjectImmediate(item);
        }
    }

    private static void RemoveRingGeometry()
    {
        var old = new List<GameObject>();
        foreach (Transform item in GameObject.Find("Workstations").GetComponentsInChildren<Transform>(true))
            if (item.name.StartsWith("Cook Ring Counter ", StringComparison.Ordinal)
                || item.name.StartsWith("Cook Ring Outer Support ", StringComparison.Ordinal))
                old.Add(item.gameObject);
        foreach (var item in old) Undo.DestroyObjectImmediate(item);
    }

    private static void Move(Transform item, Vector3 position, Quaternion rotation)
    {
        Undo.RecordObject(item, "Position circular workstation object");
        item.SetPositionAndRotation(position, rotation);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
