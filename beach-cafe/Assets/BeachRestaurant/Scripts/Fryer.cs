using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public class Fryer : MonoBehaviour
{
    private readonly Dictionary<Collider, FryerCookable> contacts = new Dictionary<Collider, FryerCookable>();
    private readonly Dictionary<FryerCookable, int> counts = new Dictionary<FryerCookable, int>();
    private BoxCollider zone;
    private AudioSource audioSource;
    private AudioClip readyTone;
    private ParticleSystem bubbles;
    private GameObject effectObject;
    private Material bubbleMaterial;
    private Texture2D bubbleTexture;

    public IEnumerable<FryerCookable> CookingItems => counts.Keys;

    // Clear contacts immediately when a portion is retrieved, rather than waiting
    // for the next physics tick after its teleport.
    public void RemoveCookable(FryerCookable item)
    {
        if (!counts.Remove(item)) return;
        var removed = new List<Collider>();
        foreach (var contact in contacts)
            if (contact.Value == item) removed.Add(contact.Key);
        foreach (var collider in removed) contacts.Remove(collider);
        if (!item) return;
        item.Ready -= OnReady;
        item.ExitFryer(this);
    }

    private void Awake()
    {
        zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
        readyTone = CreateReadyTone();
        CreateBubbleEffect();
    }

    private void OnTriggerEnter(Collider other)
    {
        var item = other.GetComponentInParent<FryerCookable>();
        if (!item || contacts.ContainsKey(other)) return;

        contacts.Add(other, item);
        if (counts.TryGetValue(item, out int count)) counts[item] = count + 1;
        else
        {
            counts.Add(item, 1);
            item.EnterFryer(this);
            item.Ready += OnReady;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!contacts.TryGetValue(other, out var item)) return;
        contacts.Remove(other);
        if (!item || --counts[item] > 0) return;

        counts.Remove(item);
        item.Ready -= OnReady;
        item.ExitFryer(this);
    }

    private void Update()
    {
        if (!bubbles) return;
        var emission = bubbles.emission;
        emission.rateOverTime = counts.Count > 0 ? 16f : 0f;
        if (counts.Count > 0 && !bubbles.isPlaying) bubbles.Play();
        else if (counts.Count == 0 && bubbles.isPlaying) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnReady() => audioSource.PlayOneShot(readyTone);

    private void OnDisable()
    {
        foreach (var item in counts.Keys)
        {
            if (!item) continue;
            item.Ready -= OnReady;
            item.ExitFryer(this);
        }
        contacts.Clear();
        counts.Clear();
        if (bubbles) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy()
    {
        if (readyTone) Destroy(readyTone);
        if (effectObject) Destroy(effectObject);
        if (bubbleMaterial) Destroy(bubbleMaterial);
        if (bubbleTexture) Destroy(bubbleTexture);
    }

    private static AudioClip CreateReadyTone()
    {
        const int sampleRate = 44100;
        var samples = new float[sampleRate / 3];
        for (int i = 0; i < samples.Length; i++)
        {
            float envelope = Mathf.Min(1f, i / 220f, (samples.Length - i) / 220f);
            float time = (float)i / sampleRate;
            float frequency = time < 0.12f ? 880f : 1175f;
            samples[i] = 0.22f * envelope * Mathf.Sin(2f * Mathf.PI * frequency * time);
        }
        var clip = AudioClip.Create("Fryer ready", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void CreateBubbleEffect()
    {
        effectObject = new GameObject("Hot oil bubbles");
        effectObject.transform.SetParent(transform, false);
        effectObject.transform.localPosition = zone.center + Vector3.up * zone.size.y * 0.5f;
        bubbles = effectObject.AddComponent<ParticleSystem>();

        var main = bubbles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(1f, 0.78f, 0.3f, 0.72f);
        main.maxParticles = 48;

        var emission = bubbles.emission;
        emission.rateOverTime = 0f;
        var shape = bubbles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(zone.size.x * 0.7f, 0.005f, zone.size.z * 0.7f);
        var fade = bubbles.colorOverLifetime;
        fade.enabled = true;
        var alpha = new Gradient();
        alpha.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;

        bubbleTexture = CreateBubbleTexture();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        if (shader)
        {
            bubbleMaterial = new Material(shader);
            bubbleMaterial.SetTexture("_BaseMap", bubbleTexture);
            bubbleMaterial.SetTexture("_MainTex", bubbleTexture);
            bubbleMaterial.SetColor("_BaseColor", Color.white);
            bubbleMaterial.SetColor("_Color", Color.white);
            bubbleMaterial.SetFloat("_Surface", 1f);
            bubbleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            bubbleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            bubbleMaterial.SetInt("_ZWrite", 0);
            bubbleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bubbleMaterial.renderQueue = (int)RenderQueue.Transparent;
            bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial = bubbleMaterial;
        }
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static Texture2D CreateBubbleTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Soft oil bubble";
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
}
