using UnityEngine;

[RequireComponent(typeof(FryerCookable))]
public class FryerCookableProgressGradient : MonoBehaviour
{
    [Min(0.01f)] public float progressRange = 1.5f;
    public Gradient gradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(new Color(1f, 0.92f, 0.72f), 0.4f / 1.5f),
            new GradientColorKey(new Color(1f, 0.72f, 0.24f), 0.8f / 1.5f),
            new GradientColorKey(new Color(0.72f, 0.36f, 0.10f), 1.2f / 1.5f),
            new GradientColorKey(new Color(0.07f, 0.045f, 0.025f), 1f)
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };

    private FryerCookable cookable;
    private Renderer[] renderers;
    private MaterialPropertyBlock properties;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (!cookable) cookable = GetComponent<FryerCookable>();
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Initialize();
        if (!cookable) return;
        cookable.ProgressChanged -= UpdateColor;
        cookable.ProgressChanged += UpdateColor;
        UpdateColor(cookable.fryProgress);
    }

    private void OnDisable()
    {
        if (cookable) cookable.ProgressChanged -= UpdateColor;
    }

    private void UpdateColor(float progress)
    {
        Color color = gradient.Evaluate(Mathf.Clamp01(progress / Mathf.Max(0.01f, progressRange)));
        foreach (var target in renderers)
        {
            if (!target) continue;
            target.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            properties.SetColor(ColorProperty, color);
            target.SetPropertyBlock(properties);
        }
    }
}
