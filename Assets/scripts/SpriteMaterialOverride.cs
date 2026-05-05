using UnityEngine;

[ExecuteAlways]
public class SpriteMaterialOverride : MonoBehaviour
{
    [Tooltip("Material to apply to all SpriteRenderers under this GameObject.")]
    public Material material;

    [Tooltip("Also apply to a SpriteRenderer on this GameObject (if any).")]
    public bool includeSelf = false;

    [Tooltip("Include inactive children.")]
    public bool includeInactive = true;

    [Tooltip("Use sharedMaterial (recommended for editor-time overrides). If false, assigns material (instance) at runtime.")]
    public bool useSharedMaterial = true;

    private void OnValidate()
    {
        Apply();
    }

    private void Awake()
    {
        // Keep scene view / play mode consistent when entering play mode.
        Apply();
    }

    [ContextMenu("Apply Material To Children")]
    public void Apply()
    {
        if (material == null)
        {
            return;
        }

        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);
        foreach (var sr in spriteRenderers)
        {
            if (!includeSelf && sr.gameObject == gameObject)
            {
                continue;
            }

            if (useSharedMaterial)
            {
                sr.sharedMaterial = material;
            }
            else
            {
                sr.material = material;
            }
        }
    }
}

