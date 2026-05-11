using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody2D rb;
    public PlayerInput playerInput;
    public Animator anim;
    public Combat combat;
    public Stamina stamina;
    public Health health;

    [Header("FSM")]
    public PlayerIdleState idleState;
    public PlayerMoveState moveState;
    public PlayerJumpState jumpState;
    public PlayerAttackState attackState;
    public PlayerDashState dashState;
    public PlayerLifeDrainState lifeDrainState;
    public PlayerState currentState;

    [Header("Movement Variables")]
    public float speed;
    public float jumpForce;
    public float jumpCutMultiplier = 0.5f;
    public float normalGravity;
    public float fallGravity;
    public float jumpGravity;
    public int facingDirection = 1;
    //Inputs
    public Vector2 moveInput;

    [Header("Jump Feel (Celeste-like)")]
    [Tooltip("Allows jumping for a short time after walking off a ledge. (Celeste 'coyote time' is ~0.1s)")]
    public float coyoteTime = 0.1f;
    [Tooltip("Queues a jump pressed slightly before landing. (Celeste jump buffer is ~0.1s)")]
    public float jumpBufferTime = 0.1f;
    [Tooltip("While holding jump near the apex, gravity is scaled by this multiplier. (Celeste uses half gravity)")]
    [Range(0.05f, 1f)]
    public float apexHangGravityMultiplier = 0.5f;
    [Tooltip("How close to 0 vertical speed counts as the jump apex for apex hang. (Tune to your units)")]
    public float apexHangVelocityThreshold = 1f;
    [Tooltip("Optional clamp for vertical speed (terminal velocity). Set <= 0 to disable.")]
    public float maxFallSpeed = 0f;
    [Tooltip("Optional clamp for max upward speed. Set <= 0 to disable.")]
    public float maxRiseSpeed = 0f;

    [HideInInspector] public float coyoteTimer;
    [HideInInspector] public float jumpBufferTimer;
    [HideInInspector] public bool jumpHeld;
    [HideInInspector] public bool jumpCutQueued;
    [HideInInspector] public bool attackPressed;
    [HideInInspector] public bool dashPressed;
    [HideInInspector] public bool lifeDrainPressed;

    [Header("Dash Settings")]
    public float dashSpeed = 14f;
    public float dashDuration = 0.15f;
    public float dashCost = 20f;
    public float dashCooldown = 0.5f;
    [Tooltip("Seconds to smoothly reduce horizontal velocity after the dash ends. Set to 0 for instant stop.")]
    public float dashEndEaseOutDuration = 0.06f;
    public float dashEndLag = 0f;
    public bool dashIgnoreGravity = false;
    [Tooltip("If false, dash forces vertical velocity to 0 during the dash (more classic horizontal air-dash).")]
    public bool dashPreserveVerticalVelocity = false;

    [Header("Dash Animation Params (Optional)")]
    [Tooltip("Trigger parameter to start the dash animation. Leave empty if unused.")]
    public string dashTriggerParam = "";
    [Tooltip("Bool parameter for 'isDashing'. Leave empty if unused.")]
    public string dashBoolParam = "isDashing";
    [Tooltip("Seconds to keep the dash animation bool true so the dash clip can play fully (even if the dash gameplay state ends earlier).")]
    public float dashAnimHoldSeconds = 0.71f;

    private float nextDashTime;

    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public float DashCost => dashCost;
    public float DashCooldown => dashCooldown;
    public float DashEndEaseOutDuration => dashEndEaseOutDuration;
    public float DashEndLag => dashEndLag;
    public bool DashIgnoreGravity => dashIgnoreGravity;
    public bool DashPreserveVerticalVelocity => dashPreserveVerticalVelocity;
    public bool CanDash => Time.time >= nextDashTime && !(currentState is PlayerDashState) && stamina != null && stamina.HasStamina(dashCost);

    [Header("Debug")]
    [Tooltip("Logs input callbacks (useful to verify PlayerInput 'Send Messages' is wired).")]
    public bool logInputCallbacks = false;

    [Header("Damage / Invincibility")]
    [Tooltip("Seconds of invincibility after taking damage.")]
    public float invincibilityDuration = 0.5f;

    private bool isInvincible;
    private float invincibilityTimer;

    private float knockbackLockTimer;
    public bool IsKnockbackLocked => knockbackLockTimer > 0f;

    [Header("Combat Animation Params (Optional)")]
    [Tooltip("Trigger parameter to start the attack animation. Leave empty if your Animator uses a different setup.")]
    public string attackTriggerParam = "";
    [Tooltip("Bool parameter for 'isAttacking'. Leave empty if unused.")]
    public string attackBoolParam = "isAttacking";
    [Tooltip("Seconds to keep the attack animation bool true so the attack clip can play fully (even if the attack gameplay state ends earlier).")]
    public float attackAnimHoldSeconds = 0.5f;

    private Coroutine animatorBoolHoldRoutine;

    [Header("Life Drain")]
    public Transform drainCheckPoint;
    public float drainCheckRadius = 0.35f;
    public LayerMask drainableLayer;
    [HideInInspector] public DrainableCorpse currentDrainTarget;

    [Header("Life Drain Animation Params (Optional)")]
    [Tooltip("Trigger parameter to start the life drain animation. Leave empty if unused.")]
    public string lifeDrainTriggerParam = "";
    [Tooltip("Bool parameter for 'isLifeDraining'. Leave empty if unused.")]
    public string lifeDrainBoolParam = "";

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    private bool isGrounded;

    private static readonly int IsGroundedParam = Animator.StringToHash("isGrounded");
    private static readonly int IsJumpingParam = Animator.StringToHash("isJumping");
    private static readonly int YVelocityParam = Animator.StringToHash("yVelocity");


    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
        if (combat == null)
        {
            combat = GetComponent<Combat>();
            if (combat == null)
            {
                combat = GetComponentInChildren<Combat>();
            }
        }
        if (stamina == null)
        {
            stamina = GetComponent<Stamina>();
            if (stamina == null)
            {
                stamina = GetComponentInChildren<Stamina>();
            }
        }
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                health = GetComponentInChildren<Health>();
            }
        }

        rb.gravityScale = normalGravity;

        idleState = new PlayerIdleState(this);
        moveState = new PlayerMoveState(this);
        jumpState = new PlayerJumpState(this);
        attackState = new PlayerAttackState(this);
        dashState = new PlayerDashState(this);
        lifeDrainState = new PlayerLifeDrainState(this);

        ChangeState(idleState);
    }

    public void HoldAnimatorBool(string boolParam, float seconds)
    {
        if (anim == null || string.IsNullOrWhiteSpace(boolParam))
        {
            return;
        }

        if (animatorBoolHoldRoutine != null)
        {
            StopCoroutine(animatorBoolHoldRoutine);
            animatorBoolHoldRoutine = null;
        }

        anim.SetBool(boolParam, true);

        if (seconds > 0f)
        {
            animatorBoolHoldRoutine = StartCoroutine(HoldAnimatorBoolRoutine(boolParam, seconds));
        }
    }

    private System.Collections.IEnumerator HoldAnimatorBoolRoutine(string boolParam, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (anim != null && !string.IsNullOrWhiteSpace(boolParam))
        {
            anim.SetBool(boolParam, false);
        }

        animatorBoolHoldRoutine = null;
    }

    public void ChangeState(PlayerState newState)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }

        currentState = newState;
        currentState.Enter();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        SyncAnimatorMovementParams();
        currentState?.FixedUpdate();
    }

    void Update()
    {
        currentState?.Update();

        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0f)
            {
                isInvincible = false;
                if (logInputCallbacks)
                {
                    Debug.Log("Player is no longer invincible", this);
                }
            }
        }

        if (knockbackLockTimer > 0f)
        {
            knockbackLockTimer -= Time.deltaTime;
        }
    }

    private void LateUpdate()
    {
        // One-frame button press semantics for state transitions.
        attackPressed = false;
        dashPressed = false;
        lifeDrainPressed = false;
    }

    public bool IsGrounded => isGrounded;

    public void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void SyncAnimatorMovementParams()
    {
        if (anim == null || rb == null)
            return;

        // These parameters exist in `Assets/sprites/animations/character animations/sprite.controller`.
        // If a different controller is used, Unity will ignore unknown parameters.
        anim.SetBool(IsGroundedParam, isGrounded);
        anim.SetBool(IsJumpingParam, !isGrounded);
        anim.SetFloat(YVelocityParam, rb.linearVelocity.y);
    }

    public void Flip()
    {
        if(moveInput.x > 0.1d)
        {
            facingDirection = 1;
        }
        else if(moveInput.x < -0.1d)
        {
            facingDirection = -1;
        }

        transform.localScale = new Vector3(facingDirection, 1, 1);
    }



    public void OnMove (InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpHeld = true;
            jumpBufferTimer = jumpBufferTime;
        }
        else //button is released
        {
            jumpHeld = false;
            jumpCutQueued = true;
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
        {
            if (logInputCallbacks)
            {
                Debug.Log("OnAttack fired (pressed)", this);
            }
            attackPressed = true;
        }
    }

    // Reuse the existing "Sprint" action as Dash for now (Input System Send Messages calls OnSprint).
    public void OnSprint(InputValue value)
    {
        if (value.isPressed)
        {
            if (logInputCallbacks)
            {
                Debug.Log("OnSprint fired (pressed) -> Dash", this);
            }
            dashPressed = true;
        }
    }

    // Reuse existing "Interact" action as Life Drain input (Input System Send Messages calls OnInteract).
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            if (logInputCallbacks)
            {
                Debug.Log("OnInteract fired (pressed) -> LifeDrain", this);
            }
            lifeDrainPressed = true;
        }
    }

    public void OnAttackAnimationFinished()
    {
        if (currentState is PlayerAttackState attack)
        {
            attack.OnAttackAnimationFinished();
        }
    }

    public void StartDashCooldown()
    {
        nextDashTime = Time.time + Mathf.Max(0f, dashCooldown);
    }

    public bool TryTakeDamage(int damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        if (isInvincible)
        {
            if (logInputCallbacks)
            {
                Debug.Log("Player is invincible", this);
            }
            return false;
        }

        if (health == null || health.IsDead)
        {
            return false;
        }

        health.TakeDamage(damage);

        if (invincibilityDuration > 0f)
        {
            isInvincible = true;
            invincibilityTimer = invincibilityDuration;
            if (logInputCallbacks)
            {
                Debug.Log("Player is invincible", this);
            }
        }

        return true;
    }

    public void StartKnockbackLock(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        knockbackLockTimer = Mathf.Max(knockbackLockTimer, duration);
    }

    public DrainableCorpse GetDrainableCorpse()
    {
        currentDrainTarget = null;

        if (drainCheckPoint == null)
        {
            return null;
        }

        Collider2D hit = Physics2D.OverlapCircle(drainCheckPoint.position, drainCheckRadius, drainableLayer);
        if (hit == null)
        {
            return null;
        }

        DrainableCorpse corpse = hit.GetComponentInParent<DrainableCorpse>();
        if (corpse == null || corpse.IsDrained)
        {
            return null;
        }

        currentDrainTarget = corpse;
        return corpse;
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        if (drainCheckPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(drainCheckPoint.position, drainCheckRadius);
        }
    }
}
