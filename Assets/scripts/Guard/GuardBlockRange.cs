using UnityEngine;

public class GuardBlockRange : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private GuardAnimationDriver animationDriver;
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [SerializeField] private float blockRange = 2.5f;
    [SerializeField] private float releaseRange = 2.75f;
    [SerializeField] private bool useHorizontalDistanceOnly = false;

    [Header("Debug")]
    [SerializeField] private bool debugBlocking;
    [SerializeField] private float debugPlayerDistance;

    private bool isBlocking;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyBlocking(false);
    }

    private void OnDisable()
    {
        ApplyBlocking(false);
    }

    private void Update()
    {
        if (playerTarget == null)
        {
            ResolvePlayer();
        }

        if (playerTarget == null || animationDriver == null)
        {
            ApplyBlocking(false);
            return;
        }

        debugPlayerDistance = GetPlayerDistance();
        float exitRange = Mathf.Max(blockRange, releaseRange);
        float activeRange = isBlocking ? exitRange : blockRange;
        ApplyBlocking(debugPlayerDistance <= activeRange);
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animationDriver == null)
        {
            animationDriver = GetComponent<GuardAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<GuardAnimationDriver>();
            }
        }

        animationDriver.Initialize(animator);

        if (playerTarget == null)
        {
            ResolvePlayer();
        }
    }

    private void ResolvePlayer()
    {
        player foundPlayer = FindAnyObjectByType<player>();
        playerTarget = foundPlayer != null ? foundPlayer.transform : null;
    }

    private float GetPlayerDistance()
    {
        Vector2 toPlayer = playerTarget.position - transform.position;
        return useHorizontalDistanceOnly ? Mathf.Abs(toPlayer.x) : toPlayer.magnitude;
    }

    private void ApplyBlocking(bool active)
    {
        debugBlocking = active;

        if (isBlocking == active)
        {
            return;
        }

        isBlocking = active;

        if (animationDriver != null)
        {
            animationDriver.SetBlocking(active);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, blockRange);
    }
}
