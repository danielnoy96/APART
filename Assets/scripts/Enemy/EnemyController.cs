using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Patrol,
        Chase,
        Dead
    }

    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private Transform player;
    [SerializeField] private ContactDamage contactDamage;
    [SerializeField] private KnockbackReceiver knockbackReceiver;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("If true, enemy stops when within Stop Distance of the player (good for later melee attacks). If false, enemy approaches until its contact-damage sensor overlaps the player.")]
    [SerializeField] private bool stopAtDistance = true;
    [SerializeField] private float stopDistance = 1.2f;

    [Header("Jump")]
    [Tooltip("Upward velocity applied when the enemy jumps.")]
    [SerializeField] private float jumpVelocity = 6f;
    [Tooltip("Seconds between jumps to prevent spamming into walls.")]
    [SerializeField] private float jumpCooldownSeconds = 0.8f;
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [Header("Obstacle Check")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Jump To Player")]
    [Tooltip("If the player is above the enemy by at least this many units, GOAP can choose to jump to reach them.")]
    [SerializeField] private float playerAboveMinDeltaY = 1.25f;
    [Tooltip("Only consider jumping to the player if horizontal distance is within this range (prevents random jumps).")]
    [SerializeField] private float playerAboveMaxDeltaX = 2.5f;
    [Tooltip("How long the player must stay above before the enemy reacts with a jump.")]
    [SerializeField] private float playerAboveJumpDelay = 0.3f;

    [Header("Patrol")]
    [Tooltip("If patrolPoints is assigned (size >= 2), enemy patrols between points. Otherwise it patrols back/forth by patrolDistance.")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolDistance = 3f;
    [SerializeField] private float idleTimeAtPatrolPoint = 0.5f;

    [Header("Animator Params (Optional)")]
    [Tooltip("Bool parameter for movement (e.g. isMoving). Leave empty if unused.")]
    [SerializeField] private string moveBoolParam = "";
    [Tooltip("Float parameter for speed (e.g. speed). Leave empty if unused.")]
    [SerializeField] private string speedFloatParam = "";

    [Header("Debug")]
    [Tooltip("Logs when EnemyController is overriding velocity (useful to debug knockback being canceled).")]
    [SerializeField] private bool logVelocityOverrides = false;
    [Tooltip("Logs jump gating (grounded/cooldown/etc). Enable temporarily for diagnosing why the enemy won't jump.")]
    [SerializeField] private bool debugJump = false;

    private State state;
    private Vector2 spawnPosition;
    private int patrolIndex;
    private int patrolDirection = 1;
    private float idleUntilTime;
    private Collider2D contactDamageSensor;
    private Collider2D playerCollider;
    private Collider2D selfCollider;
    private Transform contactDamageSensorTransform;
    private Vector3 contactSensorInitialLocalPos;
    private bool contactSensorHasInitial;
    private CrashKonijn.Goap.Runtime.GoapActionProvider goapActionProvider;
    private CrashKonijn.Agent.Runtime.AgentBehaviour goapAgentBehaviour;
    private float nextJumpTime;
    private float playerAboveDetectedSince = -1f;
    private float lastMoveDir = 1f;

    public KnockbackReceiver KnockbackReceiver => knockbackReceiver;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (contactDamage == null)
        {
            contactDamage = GetComponentInChildren<ContactDamage>(true);
        }
        if (contactDamage != null)
        {
            contactDamageSensor = contactDamage.GetComponent<Collider2D>();
            contactDamageSensorTransform = contactDamage.transform;
            contactSensorInitialLocalPos = contactDamageSensorTransform.localPosition;
            contactSensorHasInitial = true;
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponentInChildren<KnockbackReceiver>(true);
        }
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (player == null)
        {
            player p = FindAnyObjectByType<player>();
            player = p != null ? p.transform : null;
        }
        if (player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                playerCollider = player.GetComponentInChildren<Collider2D>();
            }
        }

        selfCollider = GetComponent<Collider2D>();

        spawnPosition = transform.position;
        state = State.Patrol;

        goapActionProvider = GetComponent<CrashKonijn.Goap.Runtime.GoapActionProvider>();
        goapAgentBehaviour = GetComponent<CrashKonijn.Agent.Runtime.AgentBehaviour>();

        // Sensible default: if obstacle layer is unset, treat it like ground.
        if (obstacleLayer.value == 0)
        {
            obstacleLayer = groundLayer;
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void Update()
    {
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (state == State.Dead)
        {
            return;
        }

        if (knockbackReceiver != null && knockbackReceiver.IsKnockbackActive)
        {
            // Respect knockback: do not override velocity while being knocked back.
            if (logVelocityOverrides && rb != null)
            {
                Debug.Log($"EnemyController({name}) knockback active; skipping override v={rb.linearVelocity}", this);
            }
            return;
        }

        switch (state)
        {
            case State.Idle:
                SetHorizontalVelocity(0f);
                break;

            case State.Patrol:
                Patrol();
                break;

            case State.Chase:
                ChasePlayer();
                break;
        }
    }

    // Called by EnemyBrain; kept internal to avoid external state abuse.
    internal void SetStateIdle() => state = State.Idle;
    internal void SetStatePatrol() => state = State.Patrol;
    internal void SetStateChase() => state = State.Chase;

    // GOAP action will call Patrol() later.
    public void Patrol()
    {
        if (state != State.Dead)
            state = State.Patrol;

        float targetX;

        if (Time.time < idleUntilTime)
        {
            SetHorizontalVelocity(0f);
            return;
        }

        if (patrolPoints != null && patrolPoints.Length >= 2)
        {
            Transform target = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)];
            targetX = target.position.x;

            float dx = targetX - transform.position.x;
            if (Mathf.Abs(dx) <= 0.1f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                idleUntilTime = Time.time + idleTimeAtPatrolPoint;
                SetHorizontalVelocity(0f);
                return;
            }

            float dir = Mathf.Sign(dx);
            SetHorizontalVelocity(dir * moveSpeed);
            lastMoveDir = dir;
            FlipByVelocity(dir);
            return;
        }

        float left = spawnPosition.x - patrolDistance;
        float right = spawnPosition.x + patrolDistance;
        targetX = patrolDirection > 0 ? right : left;

        float delta = targetX - transform.position.x;
        if (Mathf.Abs(delta) <= 0.1f)
        {
            patrolDirection *= -1;
            idleUntilTime = Time.time + idleTimeAtPatrolPoint;
            SetHorizontalVelocity(0f);
            return;
        }

        SetHorizontalVelocity(Mathf.Sign(delta) * moveSpeed);
        lastMoveDir = Mathf.Sign(delta);
        FlipByVelocity(lastMoveDir);
    }

    // GOAP action will call ChasePlayer() later.
    public void ChasePlayer()
    {
        if (state != State.Dead)
            state = State.Chase;

        if (player == null)
        {
            state = State.Patrol;
            return;
        }

        float distanceX = player.position.x - transform.position.x;
        bool playerAboveReadyToJump = IsPlayerAboveReadyToJump();

        if (ShouldStopChasing(distanceX))
        {
            if (playerAboveReadyToJump)
            {
                TryJump();
            }

            SetHorizontalVelocity(0f);
            return;
        }

        // If the player is on a higher platform, attempt a jump while chasing.
        // This is also used as a safety net when GOAP planning is still being iterated.
        if (playerAboveReadyToJump)
        {
            TryJump();
        }

        float dir = Mathf.Sign(distanceX);
        SetHorizontalVelocity(dir * moveSpeed);
        lastMoveDir = dir;
        FlipByVelocity(dir);
    }

    // GOAP action will call StopMoving() later.
    public void StopMoving()
    {
        SetHorizontalVelocity(0f);
    }

    public bool IsGrounded()
    {
        if (groundCheck == null)
        {
            // If not configured, treat as grounded to avoid breaking gameplay; jump will still be gated by cooldown.
            return true;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    public bool IsObstacleAhead()
    {
        if (wallCheck == null)
        {
            return false;
        }

        float dir = GetFacingDirection();
        RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, new Vector2(dir, 0f), wallCheckDistance, obstacleLayer);
        return hit.collider != null;
    }

    public bool IsPlayerAbove()
    {
        if (player == null)
        {
            return false;
        }

        // Use collider feet positions when available; transform pivots can differ between prefabs
        // and make "above" trigger even when both are on the same platform.
        float enemyFeetY = selfCollider != null ? selfCollider.bounds.min.y : transform.position.y;
        float playerFeetY = playerCollider != null ? playerCollider.bounds.min.y : player.position.y;
        float deltaY = playerFeetY - enemyFeetY;
        if (deltaY < playerAboveMinDeltaY)
        {
            return false;
        }

        float deltaX = Mathf.Abs(player.position.x - transform.position.x);
        return deltaX <= playerAboveMaxDeltaX;
    }

    public bool IsPlayerAboveReadyToJump()
    {
        if (!IsPlayerAbove() || !IsGrounded())
        {
            playerAboveDetectedSince = -1f;
            return false;
        }

        if (playerAboveJumpDelay <= 0f)
        {
            return true;
        }

        if (playerAboveDetectedSince < 0f)
        {
            playerAboveDetectedSince = Time.time;
            return false;
        }

        return Time.time - playerAboveDetectedSince >= playerAboveJumpDelay;
    }

    public bool TryJump()
    {
        if (rb == null || IsDead)
        {
            if (debugJump)
                Debug.Log($"EnemyController({name}) TryJump blocked: rb={(rb != null ? "OK" : "NULL")} dead={IsDead}", this);
            return false;
        }

        if (knockbackReceiver != null && knockbackReceiver.IsKnockbackActive)
        {
            if (debugJump)
                Debug.Log($"EnemyController({name}) TryJump blocked: knockback active", this);
            return false;
        }

        if (Time.time < nextJumpTime)
        {
            if (debugJump)
                Debug.Log($"EnemyController({name}) TryJump blocked: cooldown ({nextJumpTime - Time.time:0.00}s)", this);
            return false;
        }

        if (!IsGrounded())
        {
            if (debugJump)
                Debug.Log($"EnemyController({name}) TryJump blocked: not grounded (groundCheck={(groundCheck != null ? groundCheck.name : "NULL")} r={groundCheckRadius:0.00} layerMask={groundLayer.value})", this);
            return false;
        }

        nextJumpTime = Time.time + jumpCooldownSeconds;

        // Apply an impulse-like jump by setting Y velocity.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
        if (debugJump)
            Debug.Log($"EnemyController({name}) TryJump SUCCESS: vy={jumpVelocity:0.00}", this);
        return true;
    }

    private float GetFacingDirection()
    {
        // Prefer sprite flip if present; otherwise use last movement direction.
        if (spriteRenderer != null)
        {
            return spriteRenderer.flipX ? -1f : 1f;
        }

        return Mathf.Abs(lastMoveDir) < 0.001f ? 1f : Mathf.Sign(lastMoveDir);
    }

    // GOAP action will call FaceTarget(target) later.
    public void FaceTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.001f)
        {
            return;
        }

        FlipByVelocity(Mathf.Sign(dx));
    }

    public void SetDead()
    {
        EnterDead();
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
        if (player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                playerCollider = player.GetComponentInChildren<Collider2D>();
            }
        }
    }

    public bool IsDead => state == State.Dead || (health != null && health.IsDead);

    public bool CanMove
    {
        get
        {
            if (IsDead)
            {
                return false;
            }

            return knockbackReceiver == null || !knockbackReceiver.IsKnockbackActive;
        }
    }

    private bool ShouldStopChasing(float distanceX)
    {
        if (stopAtDistance)
        {
            return Mathf.Abs(distanceX) <= stopDistance;
        }

        // "Contact" mode: stop only when the contact-damage sensor is actually overlapping the player.
        if (contactDamageSensor == null || playerCollider == null)
        {
            // Fallback to distance-based stop if we can't detect overlap reliably.
            return Mathf.Abs(distanceX) <= stopDistance;
        }

        // Collider2D.Distance works for both triggers and non-triggers.
        ColliderDistance2D d = contactDamageSensor.Distance(playerCollider);
        return d.isOverlapped || d.distance <= 0.001f;
    }

    private void SetHorizontalVelocity(float xVelocity)
    {
        if (rb == null)
        {
            return;
        }

        // GOAP actions (and other callers) may call Patrol()/ChasePlayer() directly each frame.
        // Those calls must not cancel knockback by overriding X velocity while knockback is active.
        if (knockbackReceiver != null && knockbackReceiver.IsKnockbackActive)
        {
            if (logVelocityOverrides)
            {
                Debug.Log($"EnemyController({name}) skip vx override due to knockback (requested={xVelocity}) v={rb.linearVelocity}", this);
            }
            return;
        }

        if (logVelocityOverrides)
        {
            Debug.Log($"EnemyController({name}) overriding vx -> {xVelocity} (knockActive={(knockbackReceiver != null && knockbackReceiver.IsKnockbackActive)}) prev={rb.linearVelocity}", this);
        }
        rb.linearVelocity = new Vector2(xVelocity, rb.linearVelocity.y);
    }

    private void FlipByVelocity(float dir)
    {
        if (Mathf.Abs(dir) < 0.001f)
        {
            return;
        }

        // If the contact-damage sensor is offset to one side, mirror it when flipping.
        if (contactSensorHasInitial && contactDamageSensorTransform != null)
        {
            Vector3 local = contactDamageSensorTransform.localPosition;
            float sign = dir > 0 ? 1f : -1f;
            local.x = Mathf.Abs(contactSensorInitialLocalPos.x) * sign;
            contactDamageSensorTransform.localPosition = local;
        }

        // Prefer flipping visuals only. Flipping the Rigidbody root scale can mirror collider offsets
        // and cause physics "snaps" that feel like teleporting near stopDistance/walls.
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = dir < 0f;
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (dir > 0 ? 1f : -1f);
        transform.localScale = scale;
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        float speedAbs = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;

        if (!string.IsNullOrWhiteSpace(moveBoolParam))
        {
            animator.SetBool(moveBoolParam, speedAbs > 0.05f);
        }

        if (!string.IsNullOrWhiteSpace(speedFloatParam))
        {
            animator.SetFloat(speedFloatParam, speedAbs);
        }
    }

    private void HandleDeath()
    {
        EnterDead();
    }

    private void EnterDead()
    {
        if (state == State.Dead)
        {
            return;
        }

        state = State.Dead;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        ContactDamage[] damageSources = GetComponentsInChildren<ContactDamage>(true);
        for (int i = 0; i < damageSources.Length; i++)
        {
            damageSources[i].enabled = false;
        }

        // Leave the corpse active. Enemy.cs ensures DrainableCorpse exists/enabled on death.
        enabled = false;
    }
}
