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
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyController controller;

    private float wakeFinishedAt = -1f;
    private bool popFinished;

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
        if (IsAsleep || state == AwarenessState.Waking)
            return;

        state = AwarenessState.Hiding;
        wakeFinishedAt = -1f;
        popFinished = false;
        SetPopping(false);
        StopEnemy();
    }

    public void Sleep()
    {
        state = AwarenessState.Asleep;
        wakeFinishedAt = -1f;
        popFinished = false;
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
            popFinished = false;
            SetPopping(true);
            StopEnemy();
            return false;
        }

        if (!popFinished && Time.time < wakeFinishedAt)
            return false;

        state = AwarenessState.Active;
        SetPopping(false);
        return true;
    }

    public void OnPopAnimationFinished()
    {
        if (state != AwarenessState.Waking)
            return;

        popFinished = true;
        SetPopping(false);
        state = AwarenessState.Active;
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
