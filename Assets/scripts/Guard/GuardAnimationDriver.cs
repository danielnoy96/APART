using UnityEngine;

public class GuardAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string blockBoolParam = "isBlocking";

    [Header("Debug")]
    [SerializeField] private bool logAnimationRequests = false;
    [SerializeField] private string debugLastRequest = "None";
    [SerializeField] private bool debugBlocking;

    private int blockBoolHash;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        ConfigureBlock(blockBoolParam);
    }

    public void Initialize(Animator targetAnimator)
    {
        if (targetAnimator != null)
        {
            animator = targetAnimator;
        }

        ConfigureBlock(blockBoolParam);
    }

    public void ConfigureBlock(string boolParam)
    {
        blockBoolParam = boolParam;
        blockBoolHash = ToOptionalHash(boolParam);
    }

    public void SetBlocking(bool active)
    {
        debugBlocking = active;
        RecordRequest($"Block {(active ? "on" : "off")}");

        if (animator == null || blockBoolHash == 0)
        {
            return;
        }

        animator.SetBool(blockBoolHash, active);
    }

    public void ResetAll()
    {
        SetBlocking(false);
        debugLastRequest = "ResetAll";
        LogRequest(debugLastRequest);
    }

    private static int ToOptionalHash(string param)
    {
        return string.IsNullOrWhiteSpace(param) ? 0 : Animator.StringToHash(param);
    }

    private void RecordRequest(string request)
    {
        debugLastRequest = request;
        LogRequest(request);
    }

    private void LogRequest(string request)
    {
        if (logAnimationRequests)
        {
            Debug.Log($"GuardAnimationDriver({name}) {request}", this);
        }
    }
}
