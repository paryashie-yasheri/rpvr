using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Also equips runtime interactables, including newly assembled burgers.</summary>
public sealed class InteractionOutlineManager : MonoBehaviour
{
    public XRInteractionManager interactionManager;
    public Material outlineMaterial;

    private void OnEnable()
    {
        if (!interactionManager) interactionManager = FindFirstObjectByType<XRInteractionManager>();
        if (!interactionManager) return;
        interactionManager.interactableRegistered += OnRegistered;
        interactionManager.interactableUnregistered += OnUnregistered;
        var existing = new List<IXRInteractable>();
        interactionManager.GetRegisteredInteractables(existing);
        foreach (var item in existing) Ensure(item);
    }

    private void OnRegistered(InteractableRegisteredEventArgs args) => Ensure(args.interactableObject);

    private void Ensure(IXRInteractable item)
    {
        if (!(item is XRBaseInteractable target) || !target) return;
        var outline = target.GetComponent<HoverOutline>();
        if (!outline) outline = target.gameObject.AddComponent<HoverOutline>();
        if (outlineMaterial) outline.outlineMaterial = outlineMaterial;
        outline.Rebuild();
        var binding = target.GetComponent<InteractableOutline>();
        if (!binding) binding = target.gameObject.AddComponent<InteractableOutline>();
        binding.enabled = true;
        binding.Bind();
    }

    private void OnUnregistered(InteractableUnregisteredEventArgs args)
    {
        if (!(args.interactableObject is XRBaseInteractable target) || !target) return;
        var binding = target.GetComponent<InteractableOutline>();
        if (binding) binding.enabled = false;
    }

    private void OnDisable()
    {
        if (!interactionManager) return;
        interactionManager.interactableRegistered -= OnRegistered;
        interactionManager.interactableUnregistered -= OnUnregistered;
    }
}
