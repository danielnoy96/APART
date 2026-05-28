using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody2D rb;
    public PlayerInput playerInput;
    public Animator anim;
    [SerializeField] private PlayerAnimationDriver animationDriver;
    [SerializeField] private PlayerAnimationEventRelay animationEventRelay;
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
    [HideInInspector] public bool lifeDrainHeld;

    [Header("Dash Settings")]
    public float dashSpeed = 14f;
    public float dashDuration = 0.15f;
    public float dashCost = 30f;
    public float dashCooldown = 0.5f;
    [Tooltip("Seconds to smoothly reduce horizontal velocity after the dash ends. Set to 0 for instant stop.")]
    public float dashEndEaseOutDuration = 0.06f;
    public float dashEndLag = 0f;
    public bool dashIgnoreGravity = false;
    [Tooltip("If false, dash forces vertical velocity to 0 during the dash (more classic horizontal air-dash).")]
    public bool dashPreserveVerticalVelocity = false;

    [Header("Dash Animation Params (Optional)")]
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

    [Header("Stamina Costs")]
    [Tooltip("Stamina spent instantly when starting an attack.")]
    public float attackCost = 10f;
    [Tooltip("Stamina spent instantly when performing a jump (only when the jump impulse is applied).")]
    public float jumpCost = 30f;
    [Tooltip("Stamina spent repeatedly while Life Drain is active.")]
    public float lifeDrainStaminaCostPerTick = 2f;
    [Tooltip("Seconds between stamina ticks while Life Drain is active.")]
    public float lifeDrainStaminaTickInterval = 0.2f;

    public float AttackCost => attackCost;
    public float JumpCost => jumpCost;
    public float LifeDrainStaminaCostPerTick => lifeDrainStaminaCostPerTick;
    public float LifeDrainStaminaTickInterval => lifeDrainStaminaTickInterval;

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

    [Header("Hit Animation Params (Optional)")]
    [Tooltip("Bool parameter for the knockback/hit animation. Leave empty if unused.")]
    public string hitBoolParam = "isHit";
    [Tooltip("Seconds to play the hit animation when damage is accepted but no knockback duration is supplied.")]
    public float hitAnimHoldSeconds = 0.25f;

    private bool previousLifeDrainHeld;
    private float hitAnimationTimer;
    private float hitAnimationDuration;

    [Header("Combat Animation Params (Optional)")]
    [Tooltip("Bool parameter for 'isAttacking'. Leave empty if unused.")]
    public string attackBoolParam = "isAttacking";
    [Tooltip("Seconds to keep the attack animation bool true so the attack clip can play fully (even if the attack gameplay state ends earlier).")]
    public float attackAnimHoldSeconds = 0.5f;

    private Vector3 startingScale;

    [Header("Life Drain")]
    public Transform drainCheckPoint;
    public float drainCheckRadius = 0.35f;
    public LayerMask drainableLayer;
    [HideInInspector] public DrainableCorpse currentDrainTarget;

    [Header("Life Drain Animation Params (Optional)")]
    [Tooltip("Bool parameter for 'isLifeDraining'. Leave empty if unused.")]
    public string lifeDrainBoolParam = "isLifeDraining";

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    private bool isGrounded;

    public PlayerAnimationDriver AnimationDriver => animationDriver;


    private void Awake()
    {
        startingScale = transform.localScale;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
            {
                anim = GetComponentInChildren<Animator>();
            }
        }
        if (animationDriver == null)
        {
            animationDriver = GetComponent<PlayerAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<PlayerAnimationDriver>();
            }
        }
        if (anim != null)
        {
            animationEventRelay = anim.GetComponent<PlayerAnimationEventRelay>();
            if (animationEventRelay == null)
            {
                animationEventRelay = anim.gameObject.AddComponent<PlayerAnimationEventRelay>();
            }
        }
        else if (animationEventRelay == null)
        {
            animationEventRelay = GetComponent<PlayerAnimationEventRelay>();
            if (animationEventRelay == null)
            {
                animationEventRelay = gameObject.AddComponent<PlayerAnimationEventRelay>();
            }
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

        // Scene/prefab overrides can serialize this as empty; default to the controller param we use.
        if (string.IsNullOrWhiteSpace(lifeDrainBoolParam))
        {
            lifeDrainBoolParam = "isLifeDraining";
        }
        if (string.IsNullOrWhiteSpace(hitBoolParam))
        {
            hitBoolParam = "isHit";
        }
        animationDriver.Initialize(anim);
        animationDriver.ConfigureParams(dashBoolParam, attackBoolParam, lifeDrainBoolParam, hitBoolParam);

        rb.gravityScale = normalGravity;

        idleState = new PlayerIdleState(this);
        moveState = new PlayerMoveState(this);
        jumpState = new PlayerJumpState(this);
        attackState = new PlayerAttackState(this);
        dashState = new PlayerDashState(this);
        lifeDrainState = new PlayerLifeDrainState(this);

        ChangeState(idleState);
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
        RefreshLifeDrainHeldInput();
        currentState?.FixedUpdate();
        SyncAnimationState();
    }

    void Update()
    {
        RefreshLifeDrainHeldInput();
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
        if (hitAnimationTimer > 0f)
        {
            hitAnimationTimer -= Time.deltaTime;
        }
        SyncAnimationState();
    }

    private void LateUpdate()
    {
        // One-frame button press semantics for state transitions.
        attackPressed = false;
        dashPressed = false;
        lifeDrainPressed = false;
    }

    public void ResetForRespawn(Vector3 worldPosition)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = (Vector2)worldPosition;
        }
        else
        {
            transform.position = worldPosition;
        }

        isInvincible = false;
        invincibilityTimer = 0f;
        knockbackLockTimer = 0f;
        hitAnimationTimer = 0f;
        hitAnimationDuration = 0f;
        animationDriver?.ResetAll();

        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpHeld = false;
        jumpCutQueued = false;
        attackPressed = false;
        dashPressed = false;
        lifeDrainPressed = false;
        lifeDrainHeld = false;
        previousLifeDrainHeld = false;

        PlayerCombo combo = GetComponent<PlayerCombo>();
        combo?.ResetCombo();

        ChangeState(idleState);
        SyncAnimationState();
    }

    public bool IsGrounded => isGrounded;

    public void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void SyncAnimationState()
    {
        if (animationDriver == null || rb == null)
            return;

        // These parameters exist in `Assets/sprites/animations/character animations/sprite.controller`.
        // If a different controller is used, Unity will ignore unknown parameters.
        bool hitActive = IsKnockbackLocked || hitAnimationTimer > 0f;
        bool canUseGroundLocomotion = isGrounded && !(currentState is PlayerJumpState) && !hitActive;
        bool isIdle = Mathf.Abs(moveInput.x) < 0.1f && canUseGroundLocomotion;
        bool isRunning = Mathf.Abs(moveInput.x) > 0.1f && canUseGroundLocomotion;
        animationDriver.SetLocomotion(isIdle, isRunning, isGrounded, rb.linearVelocity.y);

        animationDriver.SetHit(hitActive, GetHitAnimationNormalizedTime(hitActive));
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

        transform.localScale = new Vector3(Mathf.Abs(startingScale.x) * facingDirection, startingScale.y, startingScale.z);
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
        lifeDrainHeld = value.isPressed;

        if (value.isPressed)
        {
            if (logInputCallbacks)
            {
                Debug.Log("OnInteract fired (pressed) -> LifeDrain", this);
            }
            lifeDrainPressed = true;
        }
        else if (logInputCallbacks)
        {
            Debug.Log("OnInteract fired (released) -> Stop LifeDrain", this);
        }
    }

    private void RefreshLifeDrainHeldInput()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        InputAction interactAction = playerInput.actions.FindAction("Interact", throwIfNotFound: false);
        if (interactAction == null)
        {
            return;
        }

        bool isHeld = interactAction.IsPressed();
        if (isHeld && !previousLifeDrainHeld)
        {
            lifeDrainPressed = true;
        }

        lifeDrainHeld = isHeld;
        previousLifeDrainHeld = isHeld;
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
        StartHitAnimationHold(hitAnimHoldSeconds);

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
        StartHitAnimationHold(knockbackLockTimer, true);
    }

    private void StartHitAnimationHold(float duration, bool overrideExisting = false)
    {
        if (duration <= 0f)
        {
            return;
        }

        if (overrideExisting || duration >= hitAnimationTimer)
        {
            hitAnimationTimer = duration;
            hitAnimationDuration = duration;
        }
        else if (hitAnimationDuration <= 0f)
        {
            hitAnimationDuration = hitAnimationTimer;
        }

        animationDriver?.SetHit(true, 0f);
    }

    private float GetHitAnimationNormalizedTime(bool hitActive)
    {
        if (!hitActive || hitAnimationDuration <= 0f)
        {
            return 0f;
        }

        float remaining = Mathf.Max(0f, hitAnimationTimer);
        return Mathf.Clamp01(1f - (remaining / hitAnimationDuration));
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
