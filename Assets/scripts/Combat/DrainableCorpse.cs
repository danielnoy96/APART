using UnityEngine;
using UnityEngine.Rendering;

public class DrainableCorpse : MonoBehaviour
{
    [Header("Drain Settings")]
    [SerializeField] private int healAmount = 10;
    [Tooltip("Fixed time (seconds) required to drain 1 HP from the corpse.")]
    [SerializeField] private float secondsPerHealPoint = 0.2f;
    [SerializeField] private float drainDuration = 0.6f;
    [SerializeField] private bool destroyAfterDrain = true;

    [Header("Drain Presentation")]
    [Tooltip("Render this corpse behind the player's sprite without pushing it below foreground props.")]
    [SerializeField] private bool renderBehindDrainer = true;
    [SerializeField] private int drainerSortingOrderOffset = -1;

    public int HealAmount => healAmount;
    public float SecondsPerHealPoint => secondsPerHealPoint;
    public float DrainDuration => drainDuration;
    public bool IsDrained { get; private set; }
    public bool DestroyAfterDrain => destroyAfterDrain;

    public void RenderBehind(Component drainer)
    {
        if (!renderBehindDrainer || drainer == null)
        {
            return;
        }

        SpriteRenderer drainerRenderer = drainer.GetComponentInChildren<SpriteRenderer>(true);
        if (drainerRenderer == null)
        {
            return;
        }

        int targetSortingLayerId = drainerRenderer.sortingLayerID;
        int targetSortingOrder = Mathf.Max(GetHighestCorpseSortingOrder(), drainerRenderer.sortingOrder + drainerSortingOrderOffset);
        int targetDrainerSortingOrder = targetSortingOrder - drainerSortingOrderOffset;
        if (targetDrainerSortingOrder <= targetSortingOrder)
        {
            targetDrainerSortingOrder = targetSortingOrder + 1;
        }

        SortingGroup[] sortingGroups = GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            sortingGroups[i].sortingLayerID = targetSortingLayerId;
            sortingGroups[i].sortingOrder = targetSortingOrder;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].sortingLayerID = targetSortingLayerId;
            spriteRenderers[i].sortingOrder = targetSortingOrder;
        }

        SortingGroup[] drainerSortingGroups = drainer.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < drainerSortingGroups.Length; i++)
        {
            drainerSortingGroups[i].sortingLayerID = targetSortingLayerId;
            drainerSortingGroups[i].sortingOrder = targetDrainerSortingOrder;
        }

        SpriteRenderer[] drainerSpriteRenderers = drainer.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < drainerSpriteRenderers.Length; i++)
        {
            drainerSpriteRenderers[i].sortingLayerID = targetSortingLayerId;
            drainerSpriteRenderers[i].sortingOrder = targetDrainerSortingOrder;
        }
    }

    private int GetHighestCorpseSortingOrder()
    {
        int highest = int.MinValue;

        SortingGroup[] sortingGroups = GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            highest = Mathf.Max(highest, sortingGroups[i].sortingOrder);
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            highest = Mathf.Max(highest, spriteRenderers[i].sortingOrder);
        }

        return highest == int.MinValue ? 0 : highest;
    }

    public void ConfigureHealAmount(int newHealAmount)
    {
        healAmount = Mathf.Max(0, newHealAmount);
        secondsPerHealPoint = Mathf.Max(0f, secondsPerHealPoint);
        drainDuration = healAmount * secondsPerHealPoint;
        IsDrained = false;
    }

    public int Drain()
    {
        if (IsDrained)
        {
            return 0;
        }

        IsDrained = true;
        Debug.Log("Life drain complete");

        return healAmount;
    }

    public void DestroyCorpse()
    {
        if (!destroyAfterDrain)
        {
            return;
        }

        Debug.Log("Drainable corpse destroyed");
        Destroy(gameObject);
    }
}
