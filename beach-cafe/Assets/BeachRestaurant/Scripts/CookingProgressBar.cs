using UnityEngine;
using UnityEngine.UI;

public sealed class CookingProgressBar : MonoBehaviour
{
    public Vector3 worldOffset = new Vector3(0f, 0.24f, 0f);
    public Vector2 size = new Vector2(0.25f, 0.055f);
    public Camera viewingCamera;

    private FryerCookable fryer;
    private StoveCookable stove;
    private float Progress => fryer ? fryer.fryProgress : stove.cookProgress;
    private float ReadyStart => fryer ? fryer.readyStart : stove.readyStart;
    private bool IsCooking => fryer ? fryer.IsFrying : stove.IsCooking;
    private bool IsOvercooked => fryer ? fryer.IsOvercooked : stove.IsOvercooked;
    private RectTransform panel;
    private RectTransform fill;
    private Image fillImage;
    private Text label;
    private CookingWarningTriangle warning;
    private int lastPercent = -1;
    private string lastState;

    private void Awake()
    {
        fryer = GetComponent<FryerCookable>();
        stove = GetComponent<StoveCookable>();
        if (!fryer && !stove) { enabled = false; return; }
        var root = new GameObject("Cooking Progress", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        panel = root.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(250f, 55f);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        MakeImage("Background", panel, new Color(0.035f, 0.045f, 0.055f, 0.94f), Vector2.zero, Vector2.one);
        label = new GameObject("Status", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(panel, false);
        SetRect(label.rectTransform, new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.96f));
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 19;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        warning = new GameObject("Ready Warning", typeof(RectTransform)).AddComponent<CookingWarningTriangle>();
        warning.transform.SetParent(panel, false);
        SetRect(warning.rectTransform, new Vector2(-0.18f, 0.08f), new Vector2(-0.02f, 0.88f));
        warning.raycastTarget = false;
        var exclamation = new GameObject("Exclamation", typeof(RectTransform)).AddComponent<Text>();
        exclamation.transform.SetParent(warning.transform, false);
        SetRect(exclamation.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.7f, 0.8f));
        exclamation.text = "!";
        exclamation.font = label.font;
        exclamation.fontSize = 29;
        exclamation.fontStyle = FontStyle.Bold;
        exclamation.color = new Color(0.08f, 0.06f, 0.02f);
        exclamation.alignment = TextAnchor.MiddleCenter;
        exclamation.raycastTarget = false;

        var track = MakeImage("Track", panel, new Color(0.14f, 0.17f, 0.19f), new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.38f)).rectTransform;
        fillImage = MakeImage("Progress", track, new Color(1f, 0.68f, 0.18f), Vector2.zero, Vector2.zero);
        fill = fillImage.rectTransform;
        fill.anchorMax = new Vector2(0f, 1f);
    }

    private void LateUpdate()
    {
        bool visible = IsCooking;
        panel.gameObject.SetActive(visible);
        if (!visible) return;
        warning.gameObject.SetActive(Progress >= ReadyStart);
        warning.color = IsOvercooked ? new Color(1f, 0.29f, 0.19f) : new Color(1f, 0.8f, 0.15f);

        if (!viewingCamera) viewingCamera = Camera.main;
        panel.position = transform.position + worldOffset;
        // Cancel the food's rotation and scale so the UI stays level and readable.
        panel.rotation = viewingCamera ? Quaternion.LookRotation(panel.position - viewingCamera.transform.position, Vector3.up) : Quaternion.identity;
        Vector3 parentScale = transform.lossyScale;
        panel.localScale = new Vector3(size.x / (250f * Mathf.Max(0.0001f, Mathf.Abs(parentScale.x))), size.y / (55f * Mathf.Max(0.0001f, Mathf.Abs(parentScale.y))), 0.001f);

        bool reachedReady = Progress >= ReadyStart;
        float normalized = reachedReady ? 1f : Mathf.Clamp01(Progress / Mathf.Max(0.0001f, ReadyStart));
        fill.anchorMax = new Vector2(normalized, 1f);
        string state = IsOvercooked ? "BURNT" : reachedReady ? "READY" : IsCooking ? (fryer ? "FRYING" : "COOKING") : "PAUSED";
        int percent = IsOvercooked ? -1 : reachedReady ? 100 : Mathf.Min(99, Mathf.FloorToInt(normalized * 100f));
        if (state == lastState && percent == lastPercent) return;
        lastState = state;
        lastPercent = percent;
        label.text = IsOvercooked ? state : state + "  " + percent + "%";
        fillImage.color = IsOvercooked ? new Color(1f, 0.29f, 0.19f) : new Color(1f, 0.68f, 0.18f);
    }

    private static Image MakeImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        SetRect(image.rectTransform, min, max);
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (panel) panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panel) Destroy(panel.gameObject);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CookingWarningTriangle : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        mesh.AddVert(new Vector3(rect.center.x, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
    }
}
