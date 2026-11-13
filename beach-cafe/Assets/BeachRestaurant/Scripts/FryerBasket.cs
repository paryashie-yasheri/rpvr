using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
[RequireComponent(typeof(InteractableOutline))]
public sealed class FryerBasket : MonoBehaviour, UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter
{
    public Fryer fryer;
    public XRSimpleInteractable Interaction { get; private set; }
    public bool canProcess => isActiveAndEnabled;

    private InteractableOutline outline;
    private IXRSelectInteractor pendingHand;
    public bool IsHighlighted => outline && outline.IsHighlighted;

    private void Awake()
    {
        Interaction = GetComponent<XRSimpleInteractable>();
        outline = GetComponent<InteractableOutline>();
    }

    private void OnEnable()
    {
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

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        pendingHand = args.interactorObject;
    }

    private void LateUpdate()
    {
        if (pendingHand == null) return;
        var interactor = pendingHand;
        pendingHand = null;
        // Leave the basket's selection callbacks before handing off to the fries.
        if (Interaction.interactorsSelecting.Contains(interactor)) Interaction.interactionManager.SelectExit(interactor, Interaction);
        if (!(interactor is XRBaseInteractor hand) || !hand || !hand.isActiveAndEnabled || hand.hasSelection || !fryer) return;

        XRGrabInteractable grab = null;
        FryerCookable portion = null;
        foreach (var item in fryer.CookingItems)
        {
            if (!item || !item.isActiveAndEnabled) continue;
            var candidate = item.GetComponent<XRGrabInteractable>();
            if (!candidate || !candidate.isActiveAndEnabled || candidate.isSelected) continue;
            portion = item;
            grab = candidate;
            break;
        }
        if (!grab || !Interaction.interactionManager.CanSelect(interactor, grab)) return;

        var previousFarMode = grab.farAttachMode;
        grab.farAttachMode = InteractableFarAttachMode.Near;
        if (hand is NearFarInteractor nearFar) nearFar.interactionAttachController.ResetOffset();
        var attach = interactor.GetAttachTransform(grab);
        var body = grab.GetComponent<Rigidbody>();
        Vector3 oldPosition = grab.transform.position;
        Quaternion oldRotation = grab.transform.rotation;
        grab.transform.SetPositionAndRotation(attach.position, attach.rotation);
        if (body)
        {
            body.position = attach.position;
            body.rotation = attach.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        Interaction.interactionManager.SelectEnter(interactor, grab);
        grab.farAttachMode = previousFarMode;
        if (grab.interactorsSelecting.Contains(interactor)) fryer.RemoveCookable(portion);
        else
        {
            grab.transform.SetPositionAndRotation(oldPosition, oldRotation);
            if (body) { body.position = oldPosition; body.rotation = oldRotation; }
            Physics.SyncTransforms();
        }
    }

    private void OnDisable()
    {
        pendingHand = null;
        if (!Interaction) return;
        Interaction.selectEntered.RemoveListener(OnSelectEntered);
        Interaction.selectFilters.Remove(this);
    }
}
