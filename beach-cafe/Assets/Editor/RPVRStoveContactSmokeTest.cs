using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>Checks that only ingredients touching the solid grill surface cook.</summary>
public static class RPVRStoveContactSmokeTest
{
    [MenuItem("RPVR/Tests/Run Stove Contact Smoke Test")]
    public static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before running the stove contact smoke test.");

        new GameObject("Stove Contact Smoke Test").AddComponent<RPVRStoveContactSmokeTestRunner>();
    }
}

public sealed class RPVRStoveContactSmokeTestRunner : MonoBehaviour
{
    private GameObject testRoot;

    private IEnumerator Start()
    {
        try
        {
            yield return new WaitForSeconds(0.2f);
            BuildStack();
            yield return new WaitForSeconds(0.8f);

            var bottom = testRoot.transform.Find("Bottom Bun").GetComponent<StoveCookable>();
            var patty = testRoot.transform.Find("Patty").GetComponent<StoveCookable>();
            var top = testRoot.transform.Find("Top Bun").GetComponent<StoveCookable>();
            Require(bottom.readyStart == 0f && bottom.readyEnd == 0.1f && bottom.smokeStartProgress == 0.1f,
                "Bun readiness or smoke thresholds are not configured for fast toasting");
            var bunParticles = testRoot.transform.Find("Bottom Bun").GetComponent<StoveCookingParticles>();
            Require(bottom.IsCooking && bottom.cookProgress > 0f, "The bun touching the grill did not cook");
            Require(bunParticles.IsSmoking,
                $"The bun did not smoke: progress={bottom.cookProgress:F2}, threshold={bottom.smokeStartProgress:F2}, cooking={bottom.IsCooking}");
            Require(!patty.IsCooking && patty.cookProgress == 0f, "The stacked patty cooked without touching the grill");
            Require(!top.IsCooking && top.cookProgress == 0f, "The top bun cooked without touching the grill");

            var topBody = top.GetComponent<Rigidbody>();
            topBody.isKinematic = true;
            topBody.position = testRoot.transform.position + new Vector3(1.15f, 0.30f, 0f);
            Physics.SyncTransforms();
            topBody.isKinematic = false;
            yield return new WaitForSeconds(0.5f);
            Require(top.IsCooking && top.cookProgress > 0f, "A bun placed directly on the grill did not cook");
            Require(!patty.IsCooking && patty.cookProgress == 0f, "The patty cooked while a different bun touched the grill");
            Debug.Log("RPVR stove contact smoke test passed: grill contact cooks buns; stacked ingredients stay raw until they touch the surface.");
        }
        finally
        {
            if (testRoot) Destroy(testRoot);
            Destroy(gameObject);
        }
    }

    private void BuildStack()
    {
        testRoot = new GameObject("Isolated Stove Contact Test Objects");
        testRoot.transform.position = new Vector3(40f, 0f, 40f);

        var grill = new GameObject("Test Grill Surface");
        grill.transform.SetParent(testRoot.transform, false);
        grill.AddComponent<BoxCollider>().size = new Vector3(3f, 0.1f, 3f);
        grill.AddComponent<Stove>();

        CreateIngredient("Bottom Bun", BurgerIngredient.IngredientKind.BottomBun, 0.3f, 0.5f);
        CreateIngredient("Patty", BurgerIngredient.IngredientKind.Patty, 0.7f, 0.3f);
        CreateIngredient("Top Bun", BurgerIngredient.IngredientKind.TopBun, 1.1f, 0.5f);
    }

    private void CreateIngredient(string objectName, BurgerIngredient.IngredientKind kind, float height, float size)
    {
        var item = new GameObject(objectName);
        item.transform.SetParent(testRoot.transform, false);
        item.transform.localPosition = new Vector3(0f, height, 0f);
        item.AddComponent<BoxCollider>().size = new Vector3(size, size, size);
        var body = item.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        item.AddComponent<BurgerIngredient>().kind = kind;
        var cookable = item.GetComponent<StoveCookable>();
        cookable.secondsToCook = 1f;
        if (kind == BurgerIngredient.IngredientKind.Patty)
        {
            cookable.readyStart = 0.8f;
            cookable.readyEnd = 1.2f;
            cookable.smokeStartProgress = 1f;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
