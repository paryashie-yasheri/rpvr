using UnityEngine;

[RequireComponent(typeof(StoveCookable))]
public class StoveCookableProgressGradient : MonoBehaviour
{
    [Min(0.01f)] public float progressRange = 1.5f;
    public Gradient gradient = new Gradient
    {
        colorKeys = new[] {
            new GradientColorKey(new Color(0.85f, 0.42f, 0.43f), 0f),
            new GradientColorKey(new Color(0.36f, 0.15f, 0.07f), 2f / 3f),
            new GradientColorKey(new Color(0.025f, 0.02f, 0.015f), 1f)
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };

    private StoveCookable cookable;
    private Renderer targetRenderer;
    private MaterialPropertyBlock properties;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (!cookable) cookable = GetComponent<StoveCookable>();
        if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Initialize();
        if (!cookable) return;
        cookable.ProgressChanged -= UpdateColor;
        cookable.ProgressChanged += UpdateColor;
        UpdateColor(cookable.cookProgress);
    }

    private void OnDisable()
    {
        if (cookable) cookable.ProgressChanged -= UpdateColor;
    }

    private void UpdateColor(float progress)
    {
        if (!targetRenderer) return;
        Color color = gradient.Evaluate(Mathf.Clamp01(progress / Mathf.Max(0.01f, progressRange)));
        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor(BaseColor, color);
        properties.SetColor(ColorProperty, color);
        targetRenderer.SetPropertyBlock(properties);
    }

    public void SetGradient(Gradient value)
    {
        gradient = value;
        Initialize();
        if (cookable) UpdateColor(cookable.cookProgress);
    }
}
