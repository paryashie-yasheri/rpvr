using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
[RequireComponent(typeof(HoverOutline))]
public sealed class HoverPressButton : MonoBehaviour, IXRSelectFilter
{
    [Tooltip("Invoked in order, once for each press. Configure actions in the Inspector.")]
    public List<UnityEvent> onPressed = new List<UnityEvent> { new UnityEvent() };
    public Transform pressVisual;
    [Min(0f)] public float pressDepth = 0.004f;
    [Min(0.01f)] public float pressDuration = 0.16f;

    public XRSimpleInteractable Interaction { get; private set; }
    public bool IsHighlighted { get; private set; }
    public bool canProcess => isActiveAndEnabled;

    private HoverOutline outline;
    private Vector3 visualRestPosition;
    private float pressedAt;
    private bool animating;
    private bool initialized;

    private void Awake()
    {
        Interaction = GetComponent<XRSimpleInteractable>();
        outline = GetComponent<HoverOutline>();
        if (pressVisual) visualRestPosition = pressVisual.localPosition;
        initialized = true;
    }

    private void OnEnable()
    {
        Interaction.hoverEntered.AddListener(OnHoverEntered);
        Interaction.hoverExited.AddListener(OnHoverExited);
        Interaction.selectEntered.AddListener(OnSelectEntered);
        Interaction.selectFilters.Add(this);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (!(interactor is XRBaseInputInteractor)) return false;
        foreach (var selected in interactor.interactablesSelected)
            if (!ReferenceEquals(selected, Interaction)) return false;
        return true;
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => SetHighlight(true);
    private void OnHoverExited(HoverExitEventArgs args) => SetHighlight(Interaction.isHovered);
    private void OnSelectEntered(SelectEnterEventArgs args) => Press();

    public void Press()
    {
        if (!isActiveAndEnabled) return;
        pressedAt = Time.time;
        animating = true;
        foreach (var action in onPressed) action?.Invoke();
    }

    private void Update()
    {
        if (!animating || !pressVisual) return;
        float t = Mathf.Clamp01((Time.time - pressedAt) / Mathf.Max(0.01f, pressDuration));
        float depression = t < 0.2f ? t / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.8f);
        Vector3 offset = pressVisual.parent.InverseTransformVector(-transform.up * pressDepth * depression);
        pressVisual.localPosition = visualRestPosition + offset;
        if (t >= 1f) animating = false;
    }

    private void SetHighlight(bool active)
    {
        IsHighlighted = active;
        if (outline) outline.SetVisible(active);
    }

    private void OnDisable()
    {
        animating = false;
        if (initialized && pressVisual) pressVisual.localPosition = visualRestPosition;
        SetHighlight(false);
        if (!Interaction) return;
        Interaction.hoverEntered.RemoveListener(OnHoverEntered);
        Interaction.hoverExited.RemoveListener(OnHoverExited);
        Interaction.selectEntered.RemoveListener(OnSelectEntered);
        Interaction.selectFilters.Remove(this);
    }
}
