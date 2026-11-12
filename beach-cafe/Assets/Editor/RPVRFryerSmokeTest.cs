using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

public static class RPVRFryerSmokeTest
{
    [MenuItem("RPVR/Tests/Run Fryer Smoke Test")]
    public static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before running the fryer smoke test.");

        new GameObject("Fryer Smoke Test").AddComponent<RPVRFryerSmokeTestRunner>();
    }
}

public sealed class RPVRFryerSmokeTestRunner : MonoBehaviour
{
    private GameObject testRoot;
    private FryerCookable item;
    private bool readyWasRaised;

    private IEnumerator Start()
    {
        try
        {
            BuildTestObjects();
            item.Ready += OnReady;
            yield return new WaitForSeconds(0.9f);

            Require(item.IsFrying, "Fries inside the fryer zone did not start frying");
            Require(item.fryProgress >= 0.8f && item.fryProgress <= 1.2f, "Fry progress missed the ready window");
            Require(readyWasRaised, "Fryer did not raise the ready event");

            float progress = item.fryProgress;
            var body = item.GetComponent<Rigidbody>();
            body.position += Vector3.right * 4f;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Require(!item.IsFrying, "Fries kept frying after leaving the oil zone");
            Require(Mathf.Abs(item.fryProgress - progress) < 0.03f, "Fry progress kept increasing after removal");
            Debug.Log("RPVR fryer smoke test passed: immersion starts frying, ready event fires, and progress stops after removal.");
        }
        finally
        {
            if (item) item.Ready -= OnReady;
            if (testRoot) Destroy(testRoot);
            Destroy(gameObject);
        }
    }

    private void BuildTestObjects()
    {
        testRoot = new GameObject("Isolated Fryer Test Objects");
        testRoot.transform.position = new Vector3(40f, 0f, 40f);

        var zone = new GameObject("Test Oil Zone");
        zone.transform.SetParent(testRoot.transform, false);
        var zoneCollider = zone.AddComponent<BoxCollider>();
        zoneCollider.size = new Vector3(2f, 2f, 2f);
        zoneCollider.isTrigger = true;
        zone.AddComponent<Fryer>();

        var fries = new GameObject("Test Fries");
        fries.transform.SetParent(testRoot.transform, false);
        var body = fries.AddComponent<Rigidbody>();
        body.useGravity = false;
        fries.AddComponent<BoxCollider>().size = Vector3.one * 0.2f;
        item = fries.AddComponent<FryerCookable>();
        item.secondsToFry = 1f;
    }

    private void OnReady() => readyWasRaised = true;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
