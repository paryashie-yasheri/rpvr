using UnityEditor;
using UnityEngine;

public static class HoverOutlineSetup
{
    public static HoverOutline Configure(GameObject target)
    {
        var outline = target.GetComponent<HoverOutline>();
        if (!outline) outline = target.AddComponent<HoverOutline>();
        const string path = "Assets/BeachRestaurant/Materials/BR_HoverOutline.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var shader = Shader.Find("BeachRestaurant/Hover Outline");
            if (!shader) throw new System.InvalidOperationException("Import the hover outline shader first.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        outline.outlineMaterial = material;
        outline.outlineColor = Color.white;
        outline.outlinePixels = 2.5f;
        outline.targetRenderers = target.GetComponentsInChildren<MeshRenderer>();
        EditorUtility.SetDirty(outline);
        return outline;
    }
}
