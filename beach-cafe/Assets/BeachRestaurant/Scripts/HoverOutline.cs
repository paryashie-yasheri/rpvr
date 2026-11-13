using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class HoverOutline : MonoBehaviour
{
    public Renderer[] targetRenderers;
    public Material outlineMaterial;
    public Color outlineColor = Color.white;
    [Range(0f, 8f)] public float outlinePixels = 2.5f;
    public bool IsVisible { get; private set; }
    public int RendererCount => shells.Count;

    private readonly List<MeshRenderer> shells = new List<MeshRenderer>();
    private MaterialPropertyBlock properties;
    private Material ownedMaterial;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        Rebuild();
    }

    public void Rebuild()
    {
        bool visible = IsVisible;
        foreach (var shell in shells) if (shell) Destroy(shell.gameObject);
        shells.Clear();
        if (properties == null) properties = new MaterialPropertyBlock();
        if (ownedMaterial && outlineMaterial != ownedMaterial)
        {
            Destroy(ownedMaterial);
            ownedMaterial = null;
        }
        if (!outlineMaterial)
        {
            var shader = Shader.Find("BeachRestaurant/Hover Outline");
            if (shader) outlineMaterial = ownedMaterial = new Material(shader);
        }
        if (!outlineMaterial) { Debug.LogError("Assign a hover outline material.", this); return; }
        var sources = targetRenderers != null && targetRenderers.Length > 0 ? targetRenderers : GetComponentsInChildren<MeshRenderer>();
        foreach (var source in sources)
        {
            if (!source || !source.enabled || !source.gameObject.activeInHierarchy || source.GetComponent<OutlineGeometry>()) continue;
            var filter = source.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            var shell = new GameObject("Hover Outline");
            shell.layer = source.gameObject.layer;
            shell.transform.SetParent(source.transform, false);
            shell.AddComponent<OutlineGeometry>();
            shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            shells.Add(renderer);
        }
        SetVisible(visible);
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible && isActiveAndEnabled && shells.Count > 0;
        if (properties != null)
        {
            properties.SetColor("_OutlineColor", outlineColor);
            properties.SetFloat("_OutlinePixels", outlinePixels);
        }
        foreach (var shell in shells)
        {
            if (!shell) continue;
            if (properties != null) shell.SetPropertyBlock(properties);
            shell.enabled = IsVisible;
        }
    }

    private void OnDisable() => SetVisible(false);

    private void OnDestroy()
    {
        foreach (var shell in shells) if (shell) Destroy(shell.gameObject);
        if (ownedMaterial) Destroy(ownedMaterial);
    }
}
