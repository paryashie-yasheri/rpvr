using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>A bottom bun accepts held ingredients, then combines their colliders into one body.</summary>
public class BurgerAssembly : MonoBehaviour
{
    [Min(0.01f)] public float snapDistance = 0.20f;
    public BurgerIngredient.IngredientKind[] allowedIngredients = { BurgerIngredient.IngredientKind.Patty, BurgerIngredient.IngredientKind.TopBun };
    public BurgerIngredient PreviewIngredient { get; private set; }
    public GameObject PreviewObject => preview;
    public Rigidbody AssemblyBody => assemblyBody ? assemblyBody : GetComponent<Rigidbody>();
    public int IngredientCount => ingredients.Count;
    public bool IsComplete => closed;

    private readonly List<BurgerIngredient> ingredients = new List<BurgerIngredient>();
    private BurgerIngredient candidate;
    private XRGrabInteractable candidateGrab;
    private GameObject preview;
    private Material previewMaterial;
    private Rigidbody assemblyBody;
    private XRGrabInteractable assemblyGrab;
    private bool assembling;
    private bool closed;

    private void Awake() => ingredients.Add(GetComponent<BurgerIngredient>());

    private void Update()
    {
        if (assembling) return;
        if (candidate && candidateGrab && !candidateGrab.isSelected)
        {
            var released = candidate;
            bool valid = IsValid(released) && InRange(released);
            ClearPreview();
            if (valid) StartCoroutine(Attach(released));
            return;
        }

        BurgerIngredient best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var item in FindObjectsByType<BurgerIngredient>())
        {
            if (!IsValid(item)) continue;
            var grab = item.GetComponent<XRGrabInteractable>();
            if (!grab || !grab.isSelected || !InRange(item)) continue;
            float distance = Vector3.Distance(item.transform.position, SnapPosition(item));
            if (distance < bestDistance) { best = item; bestDistance = distance; }
        }
        if (candidate != best)
        {
            ClearPreview();
            if (best) CreatePreview(best);
        }
        if (preview && candidate)
            preview.transform.SetPositionAndRotation(SnapPosition(candidate), transform.rotation);
    }

    private bool IsValid(BurgerIngredient item)
    {
        if (!item || closed || ingredients.Contains(item)) return false;
        if (item.kind == BurgerIngredient.IngredientKind.BottomBun) return false;
        if (item.kind == BurgerIngredient.IngredientKind.TopBun && ingredients.Count == 1) return false;
        foreach (var kind in allowedIngredients) if (item.kind == kind) return true;
        return false;
    }

    private bool InRange(BurgerIngredient item) => Vector3.Distance(item.transform.position, SnapPosition(item)) <= snapDistance;

    private Vector3 SnapPosition(BurgerIngredient item)
    {
        var baseCollider = ingredients[ingredients.Count - 1].GetComponent<MeshCollider>();
        var itemCollider = item.GetComponent<MeshCollider>();
        if (!baseCollider || !itemCollider || !baseCollider.sharedMesh || !itemCollider.sharedMesh)
        {
            var baseShape = ingredients[ingredients.Count - 1].GetComponent<Collider>();
            var itemShape = item.GetComponent<Collider>();
            return baseShape.bounds.center + transform.up * (baseShape.bounds.extents.y + itemShape.bounds.extents.y)
                   - (itemShape.bounds.center - item.transform.position);
        }
        Vector3 up = transform.up;
        var baseBounds = baseCollider.sharedMesh.bounds;
        Vector3 center = baseCollider.transform.TransformPoint(baseBounds.center);
        Vector3 extent = baseBounds.extents;
        float baseHeight = Mathf.Abs(Vector3.Dot(up, baseCollider.transform.TransformVector(Vector3.right * extent.x)))
                         + Mathf.Abs(Vector3.Dot(up, baseCollider.transform.TransformVector(Vector3.up * extent.y)))
                         + Mathf.Abs(Vector3.Dot(up, baseCollider.transform.TransformVector(Vector3.forward * extent.z)));
        var itemBounds = itemCollider.sharedMesh.bounds;
        Vector3 scale = item.transform.lossyScale;
        Vector3 centerOffset = transform.rotation * Vector3.Scale(itemBounds.center, scale);
        float itemHeight = Mathf.Abs(itemBounds.extents.y * scale.y);
        return center + up * (baseHeight + itemHeight) - centerOffset;
    }

    private void CreatePreview(BurgerIngredient item)
    {
        candidate = PreviewIngredient = item;
        candidateGrab = item.GetComponent<XRGrabInteractable>();
        if (!previewMaterial)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            previewMaterial = new Material(shader);
            previewMaterial.SetFloat("_Surface", 1f);
            previewMaterial.SetFloat("_Blend", 0f);
            previewMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            previewMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            previewMaterial.SetInt("_ZWrite", 0);
            previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            previewMaterial.renderQueue = (int)RenderQueue.Transparent;
            previewMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
        }
        preview = new GameObject("Ingredient snap preview");
        foreach (var source in item.GetComponentsInChildren<MeshFilter>())
        {
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            if (!sourceRenderer || !source.sharedMesh) continue;
            var visual = new GameObject(source.name + " preview");
            visual.transform.SetParent(preview.transform, false);
            visual.transform.localPosition = item.transform.InverseTransformPoint(source.transform.position);
            visual.transform.localRotation = Quaternion.Inverse(item.transform.rotation) * source.transform.rotation;
            visual.transform.localScale = source.transform.lossyScale;
            visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            var materials = new Material[sourceRenderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = previewMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private void ClearPreview()
    {
        if (preview) Destroy(preview);
        preview = null;
        PreviewIngredient = candidate = null;
        candidateGrab = null;
    }

    private IEnumerator Attach(BurgerIngredient item)
    {
        assembling = true;
        Vector3 position = SnapPosition(item);
        Quaternion rotation = transform.rotation;
        var itemGrab = item.GetComponent<XRGrabInteractable>();
        var itemBody = item.GetComponent<Rigidbody>();
        float itemMass = itemBody.mass;
        itemGrab.enabled = false;
        itemBody.isKinematic = true;
        itemBody.detectCollisions = false;
        Rigidbody baseBody = null;
        if (!assemblyBody)
        {
            baseBody = GetComponent<Rigidbody>();
            var baseGrab = GetComponent<XRGrabInteractable>();
            baseGrab.enabled = false;
            baseBody.isKinematic = true;
            baseBody.detectCollisions = false;
            var root = new GameObject("Burger Assembly");
            root.AddComponent<ServingItem>().itemType = OrderItem.Burger;
            root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            transform.SetParent(root.transform, true);
            float baseMass = baseBody.mass;
            Destroy(baseGrab);
            assemblyBody = root.AddComponent<Rigidbody>();
            assemblyBody.mass = baseMass;
            assemblyBody.isKinematic = true;
            assemblyBody.interpolation = RigidbodyInterpolation.Interpolate;
            assemblyBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            assemblyGrab = root.AddComponent<XRGrabInteractable>();
            assemblyGrab.enabled = false;
            assemblyGrab.useDynamicAttach = true;
            assemblyGrab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        }
        item.transform.SetParent(assemblyBody.transform, true);
        item.transform.SetPositionAndRotation(position, rotation);
        Destroy(itemGrab);
        yield return null; // Grab interactables must be removed before their required rigidbodies.
        Destroy(itemBody);
        if (baseBody) Destroy(baseBody);
        yield return null;
        // XRI completes its release during the frame after deselection. Apply the final
        // pose only once it can no longer overwrite the newly attached ingredient.
        item.transform.SetPositionAndRotation(SnapPosition(item), transform.rotation);
        ingredients.Add(item);
        closed = item.kind == BurgerIngredient.IngredientKind.TopBun;
        assemblyGrab.enabled = false;
        assemblyGrab.colliders.Clear();
        foreach (var collider in assemblyBody.GetComponentsInChildren<Collider>())
            if (!collider.isTrigger) assemblyGrab.colliders.Add(collider);
        assemblyBody.mass += itemMass;
        assemblyBody.isKinematic = false;
        assemblyGrab.enabled = true;
        assembling = false;
    }

    private void OnDestroy()
    {
        ClearPreview();
        if (previewMaterial) Destroy(previewMaterial);
    }
}
