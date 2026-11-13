using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class SodaSceneSetup
{
    [MenuItem("Beach Restaurant/Setup Soda Serving")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var cup = GameObject.Find("Cup");
        if (!cup) throw new System.InvalidOperationException("Cup is missing.");
        Configure(cup);
        CounterModelsSetup.SavePrefab(cup, "Assets/BeachRestaurant/Models/SodaCup/Soda Cup.prefab");
        EditorSceneManager.MarkSceneDirty(cup.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static void Configure(GameObject cup)
    {
        var serving = cup.GetComponent<ServingItem>();
        if (!serving) serving = cup.AddComponent<ServingItem>();
        serving.itemType = OrderItem.Soda;
        var body = cup.GetComponent<Rigidbody>();
        body.mass = 0.35f;
        body.centerOfMass = new Vector3(0, 0.08f, 0);
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var grab = cup.GetComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        cup.transform.rotation = Quaternion.Euler(0, cup.transform.eulerAngles.y, 0);
        Physics.SyncTransforms();
        float bestDistance = float.PositiveInfinity;
        Vector3 restingPosition = cup.transform.position;
        foreach (var counter in Object.FindObjectsByType<BoxCollider>())
        {
            if (!counter.name.EndsWith(" Counter") && !counter.name.StartsWith("Cook Ring Counter ")) continue;
            // Clamp in the counter's own space so diagonal ring segments are handled correctly.
            var local = counter.transform.InverseTransformPoint(cup.transform.position) - counter.center;
            var scale = counter.transform.lossyScale;
            float marginX = 0.09f / Mathf.Abs(scale.x);
            float marginZ = 0.09f / Mathf.Abs(scale.z);
            var half = counter.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + marginX, half.x - marginX);
            local.z = Mathf.Clamp(local.z, -half.z + marginZ, half.z - marginZ);
            local.y = half.y;
            var point = counter.transform.TransformPoint(local + counter.center) + Vector3.up * 0.002f;
            float distance = new Vector2(point.x - cup.transform.position.x, point.z - cup.transform.position.z).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; restingPosition = point; }
        }
        if (float.IsPositiveInfinity(bestDistance))
            throw new System.InvalidOperationException("No counter surface found for the soda cup.");
        cup.transform.position = restingPosition;
        Physics.SyncTransforms();
        var shapes = cup.GetComponentsInChildren<Collider>();
        float bottom = float.PositiveInfinity;
        foreach (var shape in shapes)
            if (shape.enabled && !shape.isTrigger) bottom = Mathf.Min(bottom, shape.bounds.min.y);
        if (!float.IsPositiveInfinity(bottom)) cup.transform.position += Vector3.up * (restingPosition.y - bottom);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(grab);
        EditorUtility.SetDirty(serving);
    }
}
