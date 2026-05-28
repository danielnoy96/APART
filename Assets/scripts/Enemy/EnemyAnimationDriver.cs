using UnityEngine;

public class EnemyAnimationDriver : MonoBehaviour
{
    private Animator animator;
    private int moveBoolParam;
    private int speedFloatParam;
    private int hurtTriggerParam;
    private int deathTriggerParam;
    private int popBoolParam;
    private int unpopBoolParam;
    private string deathStateName;

    [Header("Debug")]
    [SerializeField] private bool logAnimationRequests = false;
    [SerializeField] private string debugLastRequest = "None";
    [SerializeField] private float debugSpeedAbs;
    [SerializeField] private bool debugIsMoving;
    [SerializeField] private bool debugPopping;
    [SerializeField] private bool debugUnpopping;

    public void Initialize(Animator targetAnimator)
    {
        if (targetAnimator != null)
        {
            animator = targetAnimator;
        }
    }

    public void ConfigureMovement(string moveBool, string speedFloat)
    {
        moveBoolParam = ToOptionalHash(moveBool);
        speedFloatParam = ToOptionalHash(speedFloat);
    }

    public void ConfigureDamage(string hurtTrigger, string deathTrigger, string deathState)
    {
        hurtTriggerParam = ToOptionalHash(hurtTrigger);
        deathTriggerParam = ToOptionalHash(deathTrigger);
        deathStateName = deathState;
    }

    public void ConfigureAwareness(string popBool, string unpopBool)
    {
        popBoolParam = ToOptionalHash(popBool);
        unpopBoolParam = ToOptionalHash(unpopBool);
    }

    public void SetMovement(float speedAbs)
    {
        debugSpeedAbs = speedAbs;
        debugIsMoving = speedAbs > 0.05f;

        if (animator == null)
        {
            return;
        }

        if (moveBoolParam != 0)
        {
            animator.SetBool(moveBoolParam, speedAbs > 0.05f);
        }

        if (speedFloatParam != 0)
        {
            animator.SetFloat(speedFloatParam, speedAbs);
        }
    }

    public void PlayHurt()
    {
        RecordRequest("Hurt");

        if (animator == null || hurtTriggerParam == 0)
        {
            return;
        }

        animator.SetTrigger(hurtTriggerParam);
    }

    public bool PlayDeath()
    {
        RecordRequest("Death");

        if (animator == null || deathTriggerParam == 0)
        {
            return false;
        }

        if (hurtTriggerParam != 0)
        {
            animator.ResetTrigger(hurtTriggerParam);
        }

        animator.SetTrigger(deathTriggerParam);

        if (!string.IsNullOrWhiteSpace(deathStateName))
        {
            animator.Play(deathStateName, 0, 0f);
        }

        return true;
    }

    public void SetPopping(bool active)
    {
        debugPopping = active;
        RecordRequest($"Pop {(active ? "on" : "off")}");
        SetOptionalBool(popBoolParam, active);
    }

    public void SetUnpopping(bool active)
    {
        debugUnpopping = active;
        RecordRequest($"Unpop {(active ? "on" : "off")}");
        SetOptionalBool(unpopBoolParam, active);
    }

    public void ResetAll()
    {
        if (animator == null)
        {
            return;
        }

        SetOptionalBool(moveBoolParam, false);
        if (speedFloatParam != 0)
        {
            animator.SetFloat(speedFloatParam, 0f);
        }

        SetOptionalBool(popBoolParam, false);
        SetOptionalBool(unpopBoolParam, false);

        if (hurtTriggerParam != 0)
        {
            animator.ResetTrigger(hurtTriggerParam);
        }

        if (deathTriggerParam != 0)
        {
            animator.ResetTrigger(deathTriggerParam);
        }

        debugLastRequest = "ResetAll";
        debugSpeedAbs = 0f;
        debugIsMoving = false;
        debugPopping = false;
        debugUnpopping = false;
        LogRequest(debugLastRequest);
    }

    private void SetOptionalBool(int param, bool value)
    {
        if (animator == null || param == 0)
        {
            return;
        }

        animator.SetBool(param, value);
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
            Debug.Log($"EnemyAnimationDriver({name}) {request}", this);
        }
    }
}
