using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>A typed XRI socket: preview while held, snap on release, grab to remove.</summary>
public sealed class TraySlot : XRSocketInteractor
{
    public OrderItem acceptedItem;
    public GameObject emptyHint;
    // XRI clears firstInteractableSelected after invoking selectExited listeners.
    public ServingItem Item => !hasSelection || firstInteractableSelected == null ? null : firstInteractableSelected.transform.GetComponent<ServingItem>();

    private Transform resolvedAttach;
    private Transform cachedItem;
    private Bounds localBounds;

    protected override void Awake()
    {
        base.Awake();
        resolvedAttach = new GameObject("Resolved Placement").transform;
        resolvedAttach.SetParent(transform, false);
    }

    private bool Accepts(IXRInteractable interactable)
    {
        var item = interactable.transform.GetComponent<ServingItem>();
        return item && item.itemType == acceptedItem && item.CanPlaceOnTray;
    }

    public override bool CanHover(IXRHoverInteractable interactable) => Accepts(interactable) && base.CanHover(interactable);
    public override bool CanSelect(IXRSelectInteractable interactable) => Accepts(interactable) && base.CanSelect(interactable);

    public override Transform GetAttachTransform(IXRInteractable interactable)
    {
        var surface = base.GetAttachTransform(interactable);
        if (!resolvedAttach || interactable == null) return surface;
        var root = interactable.transform;
        if (cachedItem != root)
        {
            cachedItem = root;
            bool first = true;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { localBounds = new Bounds(point, Vector3.zero); first = false; }
                    else localBounds.Encapsulate(point);
                }
            }
            if (first) localBounds = new Bounds(Vector3.zero, Vector3.zero);
        }
        var bottomCenter = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
        resolvedAttach.SetPositionAndRotation(surface.position - surface.rotation * Vector3.Scale(bottomCenter, root.lossyScale), surface.rotation);
        return resolvedAttach;
    }

    private void LateUpdate()
    {
        if (emptyHint && emptyHint.activeSelf == hasSelection) emptyHint.SetActive(!hasSelection);
    }
}
