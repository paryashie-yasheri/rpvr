using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(HoverOutline))]
public sealed class InteractableOutline : MonoBehaviour
{
    public bool IsHighlighted { get; private set; }
    private XRBaseInteractable interaction;
    private HoverOutline outline;

    private void OnEnable() => Bind();

    public void Bind()
    {
        Unbind();
        interaction = GetComponent<XRBaseInteractable>();
        outline = GetComponent<HoverOutline>();
        if (!interaction) return;
        interaction.hoverEntered.AddListener(OnHoverEntered);
        interaction.hoverExited.AddListener(OnHoverExited);
        interaction.selectEntered.AddListener(OnSelectEntered);
        interaction.selectExited.AddListener(OnSelectExited);
        Refresh();
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => Refresh();
    private void OnHoverExited(HoverExitEventArgs args) => Refresh();
    private void OnSelectEntered(SelectEnterEventArgs args) => Refresh();
    private void OnSelectExited(SelectExitEventArgs args) => Refresh();

    public void Refresh()
    {
        bool active = false;
        if (interaction && interaction.isActiveAndEnabled)
        {
            foreach (var hand in interaction.interactorsHovering)
                if (!(hand is XRSocketInteractor)) { active = true; break; }
            foreach (var hand in interaction.interactorsSelecting)
                if (!(hand is XRSocketInteractor)) { active = true; break; }
        }
        IsHighlighted = active && isActiveAndEnabled;
        if (outline) outline.SetVisible(IsHighlighted);
    }

    private void Unbind()
    {
        if (!interaction) return;
        interaction.hoverEntered.RemoveListener(OnHoverEntered);
        interaction.hoverExited.RemoveListener(OnHoverExited);
        interaction.selectEntered.RemoveListener(OnSelectEntered);
        interaction.selectExited.RemoveListener(OnSelectExited);
        interaction = null;
    }

    private void OnDisable()
    {
        Unbind();
        IsHighlighted = false;
        if (outline) outline.SetVisible(false);
    }
}
