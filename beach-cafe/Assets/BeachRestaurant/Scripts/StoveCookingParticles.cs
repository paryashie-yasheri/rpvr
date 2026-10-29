using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(StoveCookable))]
public class StoveCookingParticles : MonoBehaviour
{
    public bool IsSmoking => smoke && smoke.isEmitting;

    private StoveCookable cookable;
    private ParticleSystem steam;
    private ParticleSystem smoke;
    private GameObject effectRoot;
    private Material particleMaterial;
    private Texture2D particleTexture;

    private void Awake()
    {
        cookable = GetComponent<StoveCookable>();
        effectRoot = new GameObject(name + " cooking effects");
        particleTexture = CreateSoftParticle();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        particleMaterial = new Material(shader);
        particleMaterial.SetTexture("_BaseMap", particleTexture);
        particleMaterial.SetColor("_BaseColor", Color.white);
        particleMaterial.SetFloat("_Surface", 1f);
        particleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        particleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        particleMaterial.SetInt("_ZWrite", 0);
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMaterial.renderQueue = (int)RenderQueue.Transparent;
        steam = CreateEffect("Sizzle steam", new Color(0.95f, 0.87f, 0.68f, 0.40f), 8f, 0.05f, 1f, 0.09f);
        smoke = CreateEffect("Overcooking smoke", new Color(0.25f, 0.25f, 0.25f, 0.8f), 18f, 0.13f, 2.1f, 0.17f);
    }

    private ParticleSystem CreateEffect(string effectName, Color color, float rate, float size, float lifetime, float speed)
    {
        var obj = new GameObject(effectName);
        obj.transform.SetParent(effectRoot.transform, false);
        var system = obj.AddComponent<ParticleSystem>();
        var main = system.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 80;
        var emission = system.emission;
        emission.rateOverTime = rate;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.07f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var colors = system.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colors.color = gradient;
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.6f));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static Texture2D CreateSoftParticle()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Soft cooking particle";
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        texture.Apply();
        return texture;
    }

    private void Update()
    {
        effectRoot.transform.position = GetComponent<Collider>().bounds.center + Vector3.up * GetComponent<Collider>().bounds.extents.y;
        SetEmission(steam, cookable.IsCooking && cookable.cookProgress < cookable.smokeStartProgress);
        SetEmission(smoke, cookable.IsCooking && cookable.cookProgress >= cookable.smokeStartProgress);
    }

    private static void SetEmission(ParticleSystem system, bool active)
    {
        if (active && !system.isPlaying) system.Play();
        else if (!active && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnDisable()
    {
        if (steam) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy()
    {
        if (effectRoot) Destroy(effectRoot);
        if (particleMaterial) Destroy(particleMaterial);
        if (particleTexture) Destroy(particleTexture);
    }
}
