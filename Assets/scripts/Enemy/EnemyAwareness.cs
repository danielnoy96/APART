using UnityEngine;

public class EnemyAwareness : MonoBehaviour
{
    public enum AwarenessState
    {
        Asleep,
        Hiding,
        Waking,
        Active
    }

    [SerializeField] private AwarenessState state = AwarenessState.Hiding;
    [SerializeField] private float wakeFallbackSeconds = 0.75f;
    [SerializeField] private string popBoolParam = "isPopping";
    [SerializeField] private string popStateName = "pop";
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyController controller;

    private float wakeFinishedAt = -1f;

    public bool IsAsleep => state == AwarenessState.Asleep;
    public bool IsHiding => state == AwarenessState.Hiding;
    public bool CanRunRegularBehavior => state == AwarenessState.Active;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (controller == null)
            controller = GetComponent<EnemyController>();

        if (!CanRunRegularBehavior)
            StopEnemy();
    }

    public void Hide()
    {
        if (IsAsleep)
            return;

        state = AwarenessState.Hiding;
        wakeFinishedAt = -1f;
        SetPopping(false);
        StopEnemy();
    }

    public void Sleep()
    {
        state = AwarenessState.Asleep;
        wakeFinishedAt = -1f;
        SetPopping(false);
        StopEnemy();
    }

    public bool WakeAndReady()
    {
        if (IsAsleep)
            return false;

        if (state == AwarenessState.Active)
            return true;

        if (state == AwarenessState.Hiding)
        {
            state = AwarenessState.Waking;
            wakeFinishedAt = Time.time + Mathf.Max(0f, wakeFallbackSeconds);
            SetPopping(true);
            StopEnemy();
            return false;
        }

        if (!IsPopFinished() && Time.time < wakeFinishedAt)
            return false;

        state = AwarenessState.Active;
        SetPopping(false);
        return true;
    }

    private bool IsPopFinished()
    {
        if (animator == null || string.IsNullOrWhiteSpace(popStateName))
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(popStateName) || current.IsName($"Base Layer.{popStateName}"))
            return current.normalizedTime >= 1f;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return !(next.IsName(popStateName) || next.IsName($"Base Layer.{popStateName}"));
    }

    private void SetPopping(bool value)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(popBoolParam))
            animator.SetBool(popBoolParam, value);
    }

    private void StopEnemy()
    {
        if (controller == null)
            return;

        controller.SetStateIdle();
        controller.StopMoving();
    }
}
