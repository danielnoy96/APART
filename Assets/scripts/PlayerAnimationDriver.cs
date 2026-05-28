using System.Collections;
using UnityEngine;

public enum PlayerAnim
{
    Idle,
    Run,
    JumpRise,
    Fall,
    Dash,
    Attack,
    LifeDrain,
    Hit,
    Death
}

public class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int IsIdleParam = Animator.StringToHash("isIdle");
    private static readonly int IsRunningParam = Animator.StringToHash("isRunning");
    private static readonly int IsGroundedParam = Animator.StringToHash("isGrounded");
    private static readonly int IsJumpingParam = Animator.StringToHash("isJumping");
    private static readonly int YVelocityParam = Animator.StringToHash("yVelocity");
    private static readonly int HitState = Animator.StringToHash("hit");

    private readonly Coroutine[] timedActionRoutines = new Coroutine[System.Enum.GetValues(typeof(PlayerAnim)).Length];

    private Animator animator;
    private int dashParam;
    private int attackParam;
    private int lifeDrainParam;
    private int hitParam;

    [Header("Debug")]
    [SerializeField] private bool logAnimationRequests = false;
    [SerializeField] private string debugLocomotion = "Uninitialized";
    [SerializeField] private PlayerAnim debugLastAction = PlayerAnim.Idle;
    [SerializeField] private string debugLastRequest = "None";
    [SerializeField] private bool debugTimedActionActive;
    [SerializeField] private bool debugHitActive;
    [SerializeField, Range(0f, 1f)] private float debugHitNormalizedTime;
    [SerializeField] private float debugYVelocity;

    public void Initialize(Animator targetAnimator)
    {
        animator = targetAnimator;
    }

    public void ConfigureParams(string dash, string attack, string lifeDrain, string hit)
    {
        dashParam = ToOptionalHash(dash);
        attackParam = ToOptionalHash(attack);
        lifeDrainParam = ToOptionalHash(lifeDrain);
        hitParam = ToOptionalHash(hit);
    }

    public void SetLocomotion(bool isIdle, bool isRunning, bool isGrounded, float yVelocity)
    {
        debugLocomotion = GetLocomotionDebug(isIdle, isRunning, isGrounded, yVelocity);
        debugYVelocity = yVelocity;

        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsIdleParam, isIdle);
        animator.SetBool(IsRunningParam, isRunning);
        animator.SetBool(IsGroundedParam, isGrounded);
        animator.SetBool(IsJumpingParam, !isGrounded);
        animator.SetFloat(YVelocityParam, yVelocity);
    }

    public void SetHit(bool active)
    {
        SetHit(active, 0f);
    }

    public void SetHit(bool active, float normalizedTime)
    {
        debugHitActive = active;
        debugHitNormalizedTime = active ? Mathf.Clamp01(normalizedTime) : 0f;
        SetOptionalBool(hitParam, active);

        if (animator != null && active && hitParam != 0)
        {
            float frameTime = debugHitNormalizedTime < 0.5f ? 0f : 0.5f;
            animator.Play(HitState, 0, frameTime);
        }
    }

    public void SetAction(PlayerAnim anim, bool active)
    {
        StopTimedAction(anim);
        debugLastAction = anim;
        debugTimedActionActive = false;
        RecordRequest($"{anim} {(active ? "on" : "off")}");

        int param = GetParam(anim);
        if (param != 0)
        {
            SetOptionalBool(param, active);
        }
    }

    public void PlayTimedAction(PlayerAnim anim, float seconds)
    {
        StopTimedAction(anim);
        debugLastAction = anim;
        debugTimedActionActive = seconds > 0f;
        RecordRequest($"{anim} timed {seconds:0.###}s");

        int param = GetParam(anim);
        if (param == 0)
        {
            return;
        }

        SetOptionalBool(param, true);

        if (seconds > 0f)
        {
            timedActionRoutines[(int)anim] = StartCoroutine(TimedActionRoutine(anim, seconds));
        }
    }

    public void ResetAll()
    {
        for (int i = 0; i < timedActionRoutines.Length; i++)
        {
            if (timedActionRoutines[i] != null)
            {
                StopCoroutine(timedActionRoutines[i]);
                timedActionRoutines[i] = null;
            }
        }

        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsIdleParam, false);
        animator.SetBool(IsRunningParam, false);
        animator.SetBool(IsGroundedParam, false);
        animator.SetBool(IsJumpingParam, false);
        animator.SetFloat(YVelocityParam, 0f);

        SetOptionalBool(dashParam, false);
        SetOptionalBool(attackParam, false);
        SetOptionalBool(lifeDrainParam, false);
        SetOptionalBool(hitParam, false);

        debugLocomotion = "Reset";
        debugLastAction = PlayerAnim.Idle;
        debugLastRequest = "ResetAll";
        debugTimedActionActive = false;
        debugHitActive = false;
        debugHitNormalizedTime = 0f;
        debugYVelocity = 0f;
        LogRequest(debugLastRequest);
    }

    private IEnumerator TimedActionRoutine(PlayerAnim anim, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        int param = GetParam(anim);
        if (param != 0)
        {
            SetOptionalBool(param, false);
        }

        timedActionRoutines[(int)anim] = null;
        debugTimedActionActive = HasActiveTimedAction();
    }

    private void StopTimedAction(PlayerAnim anim)
    {
        int index = (int)anim;
        if (index < 0 || index >= timedActionRoutines.Length)
        {
            return;
        }

        if (timedActionRoutines[index] != null)
        {
            StopCoroutine(timedActionRoutines[index]);
            timedActionRoutines[index] = null;
        }
    }

    private int GetParam(PlayerAnim anim)
    {
        switch (anim)
        {
            case PlayerAnim.Dash:
                return dashParam;
            case PlayerAnim.Attack:
                return attackParam;
            case PlayerAnim.LifeDrain:
                return lifeDrainParam;
            case PlayerAnim.Hit:
                return hitParam;
            default:
                return 0;
        }
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
            Debug.Log($"PlayerAnimationDriver({name}) {request}", this);
        }
    }

    private bool HasActiveTimedAction()
    {
        for (int i = 0; i < timedActionRoutines.Length; i++)
        {
            if (timedActionRoutines[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetLocomotionDebug(bool isIdle, bool isRunning, bool isGrounded, float yVelocity)
    {
        if (!isGrounded)
        {
            return yVelocity >= 0f ? "JumpRise" : "Fall";
        }

        if (isRunning)
        {
            return "Run";
        }

        return isIdle ? "Idle" : "Grounded";
    }
}
