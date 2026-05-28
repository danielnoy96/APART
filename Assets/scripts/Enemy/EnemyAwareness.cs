using UnityEngine;

public class EnemyAwareness : MonoBehaviour
{
    public enum AwarenessState
    {
        Asleep,
        Hiding,
        Waking,
        ReturningToSleep,
        Active
    }

    [SerializeField] private AwarenessState state = AwarenessState.Hiding;
    [SerializeField] private float wakeFallbackSeconds = 0.75f;
    [SerializeField] private string popBoolParam = "isPopping";
    [SerializeField] private string unpopBoolParam = "isUnpopping";
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyAnimationDriver animationDriver;
    [SerializeField] private EnemyController controller;

    private float wakeFinishedAt = -1f;
    private bool popFinished;

    public bool IsAsleep => state == AwarenessState.Asleep;
    public bool IsHiding => state == AwarenessState.Hiding || state == AwarenessState.ReturningToSleep;
    public bool CanRunRegularBehavior => state == AwarenessState.Active;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animationDriver == null)
        {
            animationDriver = GetComponent<EnemyAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<EnemyAnimationDriver>();
            }
        }
        animationDriver.Initialize(animator);
        animationDriver.ConfigureAwareness(popBoolParam, unpopBoolParam);

        if (controller == null)
            controller = GetComponent<EnemyController>();

        if (!CanRunRegularBehavior)
            StopEnemy();
    }

    public void Hide()
    {
        if (IsAsleep || state == AwarenessState.Waking || state == AwarenessState.ReturningToSleep)
            return;

        wakeFinishedAt = -1f;
        popFinished = false;
        SetPopping(false);
        StopEnemy();

        if (state == AwarenessState.Active)
        {
            state = AwarenessState.ReturningToSleep;
            SetUnpopping(true);
            return;
        }

        state = AwarenessState.Hiding;
        SetUnpopping(false);
    }

    public void Sleep()
    {
        state = AwarenessState.Asleep;
        wakeFinishedAt = -1f;
        popFinished = false;
        SetPopping(false);
        SetUnpopping(false);
        StopEnemy();
    }

    public bool WakeAndReady()
    {
        if (IsAsleep)
            return false;

        if (state == AwarenessState.Active)
            return true;

        if (state == AwarenessState.Hiding || state == AwarenessState.ReturningToSleep)
        {
            BeginWaking();
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

    public void OnUnpopAnimationFinished()
    {
        if (state != AwarenessState.ReturningToSleep)
            return;

        state = AwarenessState.Hiding;
        SetUnpopping(false);
        StopEnemy();
    }

    private void BeginWaking()
    {
        state = AwarenessState.Waking;
        wakeFinishedAt = Time.time + Mathf.Max(0f, wakeFallbackSeconds);
        popFinished = false;
        SetUnpopping(false);
        SetPopping(true);
        StopEnemy();
    }

    private void SetPopping(bool value)
    {
        if (animationDriver != null)
            animationDriver.SetPopping(value);
    }

    private void SetUnpopping(bool value)
    {
        if (animationDriver != null)
            animationDriver.SetUnpopping(value);
    }

    private void StopEnemy()
    {
        if (controller == null)
            return;

        controller.SetStateIdle();
        controller.StopMoving();
    }
}
