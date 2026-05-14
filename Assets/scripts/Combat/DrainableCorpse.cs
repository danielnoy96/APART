using UnityEngine;

public class DrainableCorpse : MonoBehaviour
{
    [Header("Drain Settings")]
    [SerializeField] private int healAmount = 10;
    [Tooltip("Fixed time (seconds) required to drain 1 HP from the corpse.")]
    [SerializeField] private float secondsPerHealPoint = 0.2f;
    [SerializeField] private float drainDuration = 0.6f;
    [SerializeField] private bool destroyAfterDrain = true;

    public int HealAmount => healAmount;
    public float SecondsPerHealPoint => secondsPerHealPoint;
    public float DrainDuration => drainDuration;
    public bool IsDrained { get; private set; }
    public bool DestroyAfterDrain => destroyAfterDrain;

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
