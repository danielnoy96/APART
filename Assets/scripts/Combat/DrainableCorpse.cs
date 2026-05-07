using UnityEngine;

public class DrainableCorpse : MonoBehaviour
{
    [Header("Drain Settings")]
    [SerializeField] private int healAmount = 10;
    [SerializeField] private float drainDuration = 0.6f;
    [SerializeField] private bool destroyAfterDrain = true;

    public int HealAmount => healAmount;
    public float DrainDuration => drainDuration;
    public bool IsDrained { get; private set; }
    public bool DestroyAfterDrain => destroyAfterDrain;

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
