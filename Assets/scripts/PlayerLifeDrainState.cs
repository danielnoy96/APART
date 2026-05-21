using UnityEngine;

public class PlayerLifeDrainState : PlayerState
{
    private DrainableCorpse targetCorpse;
    private float drainEndTime;
    private float nextProgressLogTime;
    private float nextStaminaSpendTime;

    public PlayerLifeDrainState(player player) : base(player) { }

    public override void Enter()
    {
        // Only allow life drain while grounded.
        if (!IsGrounded)
        {
            TransitionOut();
            return;
        }

        targetCorpse = player.GetDrainableCorpse();
        if (targetCorpse == null)
        {
            TransitionOut();
            return;
        }

        targetCorpse.RenderBehind(player);

        // Lock player in place (horizontal), keep grounded behavior.
        ApplyIdleHorizontalVelocity();

        if (player.anim != null)
        {
            if (!string.IsNullOrWhiteSpace(player.lifeDrainBoolParam))
            {
                player.anim.SetBool(player.lifeDrainBoolParam, true);
            }
        }

        player.lifeDrainPressed = false;

        float duration = Mathf.Max(0f, targetCorpse.DrainDuration);
        drainEndTime = Time.time + duration;
        nextProgressLogTime = Time.time;
        nextStaminaSpendTime = Time.time;

        float tickCost = Mathf.Max(0f, player.LifeDrainStaminaCostPerTick);
        if (tickCost > 0f && player.stamina != null && !player.stamina.HasStamina(tickCost))
        {
            TransitionOut();
            return;
        }

        Debug.Log("Life drain in progress");
    }

    public override void Exit()
    {
        if (player.anim != null && !string.IsNullOrWhiteSpace(player.lifeDrainBoolParam))
        {
            player.anim.SetBool(player.lifeDrainBoolParam, false);
        }

        targetCorpse = null;
    }

    public override void FixedUpdate()
    {
        UpdateJumpTimers();

        // Keep the player planted.
        ApplyIdleHorizontalVelocity();
        ApplyVariableGravity();
        ClampVerticalSpeed();

        if (targetCorpse == null)
        {
            TransitionOut();
            return;
        }

        float tickInterval = Mathf.Max(0.01f, player.LifeDrainStaminaTickInterval);
        float tickCost = Mathf.Max(0f, player.LifeDrainStaminaCostPerTick);
        if (tickCost > 0f && player.stamina != null && Time.time >= nextStaminaSpendTime)
        {
            if (!player.stamina.TrySpend(tickCost))
            {
                TransitionOut();
                return;
            }

            // Advance in fixed steps to avoid "catching up" with many spends in one frame.
            nextStaminaSpendTime = Time.time + tickInterval;
        }

        // Optional: log progress periodically (avoid spamming every frame).
        if (Time.time >= nextProgressLogTime && Time.time < drainEndTime)
        {
            Debug.Log("Life drain in progress");
            nextProgressLogTime = Time.time + 0.25f;
        }

        if (Time.time < drainEndTime)
        {
            return;
        }

        int heal = targetCorpse.Drain();
        if (heal > 0 && player.health != null)
        {
            player.health.Heal(heal);
        }

        if (heal > 0 && targetCorpse.DestroyAfterDrain)
        {
            targetCorpse.DestroyCorpse();
        }

        TransitionOut();
    }

    private void TransitionOut()
    {
        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
            return;
        }

        if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            player.ChangeState(player.moveState);
        }
        else
        {
            player.ChangeState(player.idleState);
        }
    }
}
