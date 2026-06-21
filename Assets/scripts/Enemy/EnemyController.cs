using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Patrol,
        Chase,
        Charge,
        Dead
    }

    private enum ChargePhase
    {
        None,
        Windup,
        Charging,
        Recovery
    }

    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyAnimationDriver animationDriver;
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
    [Tooltip("If true, root movement colliders without an explicit material get a no-friction runtime material so velocity-driven patrol/chase is not slowed by floor friction.")]
    [SerializeField] private bool useNoFrictionMovementMaterial = true;

    [Header("Charge")]
    [Tooltip("Horizontal speed used by fixed ChargePlayer bursts. Keep this above Move Speed for charger enemies.")]
    [SerializeField] private float chargeSpeed = 5f;
    [Tooltip("Seconds the charger stays still before locking direction and starting a charge burst.")]
    [SerializeField] private float chargeStartDuration = 0.45f;
    [Tooltip("Maximum seconds for one fixed charge burst.")]
    [SerializeField] private float chargeDuration = 0.65f;
    [Tooltip("Maximum horizontal distance for one fixed charge burst. Set <= 0 to use duration only.")]
    [SerializeField] private float chargeDistance = 3.25f;
    [Tooltip("Seconds the charger stays still after a charge burst before it can wind up again.")]
    [SerializeField] private float chargeRecoveryDuration = 0.45f;
    [Tooltip("Extra seconds after recovery before a new charge windup can start.")]
    [SerializeField] private float chargeCooldownDuration = 0.5f;
    [Tooltip("If true, player attacks still damage this enemy during the active charge burst, but do not apply knockback.")]
    [SerializeField] private bool ignoreKnockbackDuringCharge = true;

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
    [Tooltip("If true, patrol movement periodically pauses before continuing. This affects normal patrol walking only, not chase or charge.")]
    [SerializeField] private bool usePatrolMovePause = false;
    [Tooltip("Seconds to keep walking during patrol before a periodic pause starts.")]
    [SerializeField] private float patrolMoveDuration = 1.5f;
    [Tooltip("Seconds to stay stopped during the periodic patrol pause.")]
    [SerializeField] private float patrolPauseDuration = 0.4f;
    [Tooltip("If true, PatrolPauseStart and PatrolPauseEnd animation events control patrol pauses. This overrides the timed patrol pause above.")]
    [SerializeField] private bool usePatrolAnimationEvents = false;
    [Tooltip("Safety fallback: maximum seconds an animation-event patrol pause can hold if PatrolPauseEnd is missing. Set <= 0 to disable fallback.")]
    [SerializeField] private float maxPatrolAnimationPauseSeconds = 1f;

    [Header("Animator Params (Optional)")]
    [Tooltip("Bool parameter for movement (e.g. isMoving). Leave empty if unused.")]
    [SerializeField] private string moveBoolParam = "";
    [Tooltip("Float parameter for speed (e.g. speed). Leave empty if unused.")]
    [SerializeField] private string speedFloatParam = "";
    [Tooltip("Bool parameter for charge windup/start animation. Leave empty if unused.")]
    [SerializeField] private string chargeStartBoolParam = "";
    [Tooltip("Bool parameter for active charge animation. Leave empty if unused.")]
    [SerializeField] private string chargingBoolParam = "";

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
    private bool playerAboveJumpConsumed;
    private float lastMoveDir = 1f;
    private float[] patrolPointWorldXs;
    private bool[] validPatrolPointAnchors;
    private ChargePhase chargePhase;
    private float chargePhaseEndTime;
    private float chargeDirection = 1f;
    private float chargeStartX;
    private float nextChargeAllowedTime;
    private bool chargeCycleStartAllowed = true;
    private bool patrolMoveTimerRunning;
    private float patrolMoveStartedTime;
    private float patrolPauseUntilTime;
    private bool isPatrolMovePaused;
    private bool isPatrolAnimationPaused;
    private float patrolAnimationPauseFallbackTime;
    private const float PatrolPointArrivalDistance = 0.1f;
    private static PhysicsMaterial2D noFrictionMovementMaterial;

    public KnockbackReceiver KnockbackReceiver => knockbackReceiver;
    public bool IsFixedChargeCycleActive => chargePhase != ChargePhase.None;
    public bool CanReceiveAttackKnockback => !ignoreKnockbackDuringCharge || chargePhase != ChargePhase.Charging;

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
        if (animationDriver == null)
        {
            animationDriver = GetComponent<EnemyAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<EnemyAnimationDriver>();
            }
        }
        animationDriver.Initialize(animator);
        animationDriver.ConfigureMovement(moveBoolParam, speedFloatParam);
        animationDriver.ConfigureCharge(chargeStartBoolParam, chargingBoolParam);
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
        ApplyMovementPhysicsMaterial();

        spawnPosition = transform.position;
        CachePatrolPointAnchors();
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
            ResetPatrolPause();

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

            case State.Charge:
                ChargePlayer();
                break;
        }
    }

    // Called by EnemyBrain; kept internal to avoid external state abuse.
    internal void SetStateIdle()
    {
        ResetChargeCycle();
        ResetPatrolPause();
        state = State.Idle;
    }

    internal void SetStatePatrol()
    {
        ResetChargeCycle();
        state = State.Patrol;
    }

    internal void SetStateChase()
    {
        ResetChargeCycle();
        ResetPatrolPause();
        state = State.Chase;
    }

    internal void SetStateCharge()
    {
        ResetPatrolPause();
        state = State.Charge;
    }

    // GOAP action will call Patrol() later.
    public void Patrol()
    {
        ResetChargeCycle();

        if (state != State.Dead)
            state = State.Patrol;

        float targetX;

        if (Time.time < idleUntilTime)
        {
            ResetPatrolPause();
            SetHorizontalVelocity(0f);
            return;
        }

        if (TryPatrolAssignedPoints())
        {
            return;
        }

        float left = spawnPosition.x - patrolDistance;
        float right = spawnPosition.x + patrolDistance;
        targetX = patrolDirection > 0 ? right : left;

        float delta = targetX - transform.position.x;
        if (Mathf.Abs(delta) <= PatrolPointArrivalDistance)
        {
            patrolDirection *= -1;
            idleUntilTime = Time.time + idleTimeAtPatrolPoint;
            ResetPatrolPause();
            SetHorizontalVelocity(0f);
            return;
        }

        if (ShouldHoldForPatrolPause())
        {
            SetHorizontalVelocity(0f);
            return;
        }

        SetHorizontalVelocity(Mathf.Sign(delta) * moveSpeed);
        lastMoveDir = Mathf.Sign(delta);
        FlipByVelocity(lastMoveDir);
    }

    private bool TryPatrolAssignedPoints()
    {
        if (!TryGetPatrolPointBounds(out float minX, out float maxX, out int nearestIndex))
        {
            return false;
        }

        if (transform.position.x < minX - PatrolPointArrivalDistance ||
            transform.position.x > maxX + PatrolPointArrivalDistance)
        {
            patrolIndex = nearestIndex;
        }

        patrolIndex = GetValidPatrolPointIndex(patrolIndex);
        float targetX = patrolPointWorldXs[patrolIndex];
        float dx = targetX - transform.position.x;

        if (Mathf.Abs(dx) <= PatrolPointArrivalDistance)
        {
            patrolIndex = GetNextValidPatrolPointIndex(patrolIndex);
            idleUntilTime = Time.time + idleTimeAtPatrolPoint;
            ResetPatrolPause();
            SetHorizontalVelocity(0f);
            return true;
        }

        float dir = Mathf.Sign(dx);
        if (ShouldHoldForPatrolPause())
        {
            SetHorizontalVelocity(0f);
            return true;
        }

        SetHorizontalVelocity(dir * moveSpeed);
        lastMoveDir = dir;
        FlipByVelocity(dir);
        return true;
    }

    private bool TryGetPatrolPointBounds(out float minX, out float maxX, out int nearestIndex)
    {
        minX = 0f;
        maxX = 0f;
        nearestIndex = -1;

        if (patrolPointWorldXs == null || validPatrolPointAnchors == null || patrolPointWorldXs.Length < 2)
        {
            return false;
        }

        int validCount = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < patrolPointWorldXs.Length; i++)
        {
            if (!IsValidPatrolPointIndex(i))
            {
                continue;
            }

            float x = patrolPointWorldXs[i];
            if (validCount == 0)
            {
                minX = x;
                maxX = x;
            }
            else
            {
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }

            float distance = Mathf.Abs(x - transform.position.x);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }

            validCount++;
        }

        return validCount >= 2;
    }

    private int GetValidPatrolPointIndex(int startIndex)
    {
        if (patrolPointWorldXs == null || patrolPointWorldXs.Length == 0)
        {
            return 0;
        }

        int index = Mathf.Clamp(startIndex, 0, patrolPointWorldXs.Length - 1);
        if (IsValidPatrolPointIndex(index))
        {
            return index;
        }

        for (int offset = 1; offset < patrolPointWorldXs.Length; offset++)
        {
            int forward = (index + offset) % patrolPointWorldXs.Length;
            if (IsValidPatrolPointIndex(forward))
            {
                return forward;
            }
        }

        return index;
    }

    private int GetNextValidPatrolPointIndex(int currentIndex)
    {
        if (patrolPointWorldXs == null || patrolPointWorldXs.Length == 0)
        {
            return 0;
        }

        for (int offset = 1; offset <= patrolPointWorldXs.Length; offset++)
        {
            int nextIndex = (currentIndex + offset) % patrolPointWorldXs.Length;
            if (IsValidPatrolPointIndex(nextIndex))
            {
                return nextIndex;
            }
        }

        return currentIndex;
    }

    private bool ShouldHoldForPatrolPause()
    {
        if (ShouldHoldForPatrolAnimationPause())
        {
            return true;
        }

        return ShouldHoldForPatrolMovePause();
    }

    private bool ShouldHoldForPatrolAnimationPause()
    {
        if (!usePatrolAnimationEvents)
        {
            ResetPatrolAnimationPause();
            return false;
        }

        ResetPatrolMovePause();

        if (!isPatrolAnimationPaused)
        {
            return false;
        }

        float fallbackSeconds = Mathf.Max(0f, maxPatrolAnimationPauseSeconds);
        if (fallbackSeconds > 0f && Time.time >= patrolAnimationPauseFallbackTime)
        {
            ResetPatrolAnimationPause();
            return false;
        }

        isPatrolMovePaused = true;
        return true;
    }

    private bool ShouldHoldForPatrolMovePause()
    {
        if (!usePatrolMovePause || usePatrolAnimationEvents)
        {
            ResetPatrolMovePause();
            return false;
        }

        float moveSeconds = Mathf.Max(0f, patrolMoveDuration);
        float pauseSeconds = Mathf.Max(0f, patrolPauseDuration);
        if (moveSeconds <= 0f || pauseSeconds <= 0f)
        {
            ResetPatrolMovePause();
            return false;
        }

        if (Time.time < patrolPauseUntilTime)
        {
            isPatrolMovePaused = true;
            return true;
        }

        if (patrolPauseUntilTime > 0f)
        {
            patrolPauseUntilTime = 0f;
            patrolMoveStartedTime = Time.time;
            patrolMoveTimerRunning = true;
            isPatrolMovePaused = false;
            return false;
        }

        if (!patrolMoveTimerRunning)
        {
            patrolMoveStartedTime = Time.time;
            patrolMoveTimerRunning = true;
            isPatrolMovePaused = false;
            return false;
        }

        if (Time.time - patrolMoveStartedTime < moveSeconds)
        {
            isPatrolMovePaused = false;
            return false;
        }

        patrolMoveTimerRunning = false;
        patrolPauseUntilTime = Time.time + pauseSeconds;
        isPatrolMovePaused = true;
        return true;
    }

    private void ResetPatrolMovePause()
    {
        patrolMoveTimerRunning = false;
        patrolMoveStartedTime = 0f;
        patrolPauseUntilTime = 0f;
        isPatrolMovePaused = false;
    }

    private void ResetPatrolAnimationPause()
    {
        isPatrolAnimationPaused = false;
        patrolAnimationPauseFallbackTime = 0f;
        if (!isPatrolMovePaused || usePatrolAnimationEvents)
        {
            isPatrolMovePaused = false;
        }
    }

    private void ResetPatrolPause()
    {
        ResetPatrolMovePause();
        ResetPatrolAnimationPause();
    }

    private void CachePatrolPointAnchors()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolPointWorldXs = null;
            validPatrolPointAnchors = null;
            return;
        }

        patrolPointWorldXs = new float[patrolPoints.Length];
        validPatrolPointAnchors = new bool[patrolPoints.Length];

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
            {
                continue;
            }

            patrolPointWorldXs[i] = point.position.x;
            validPatrolPointAnchors[i] = true;
        }
    }

    private bool IsValidPatrolPointIndex(int index)
    {
        return validPatrolPointAnchors != null &&
               index >= 0 &&
               index < validPatrolPointAnchors.Length &&
               validPatrolPointAnchors[index];
    }

    private void ApplyMovementPhysicsMaterial()
    {
        if (!useNoFrictionMovementMaterial)
        {
            return;
        }

        if (selfCollider == null || selfCollider.isTrigger || selfCollider.sharedMaterial != null)
        {
            return;
        }

        if (noFrictionMovementMaterial == null)
        {
            noFrictionMovementMaterial = new PhysicsMaterial2D("Enemy Movement No Friction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        selfCollider.sharedMaterial = noFrictionMovementMaterial;
    }

    private void RestoreMovementPhysicsMaterial()
    {
        if (selfCollider == null || noFrictionMovementMaterial == null)
        {
            return;
        }

        if (selfCollider.sharedMaterial == noFrictionMovementMaterial)
        {
            selfCollider.sharedMaterial = null;
        }
    }

    // GOAP action will call ChasePlayer() later.
    public void ChasePlayer()
    {
        ResetChargeCycle();
        ResetPatrolPause();

        if (state != State.Dead)
            state = State.Chase;

        if (player == null)
        {
            state = State.Patrol;
            return;
        }

        float distanceX = player.position.x - transform.position.x;

        if (ShouldStopChasing(distanceX))
        {
            TryJumpToPlayerAbove();

            SetHorizontalVelocity(0f);
            return;
        }

        // If the player is on a higher platform, attempt a jump while chasing.
        // This is also used as a safety net when GOAP planning is still being iterated.
        TryJumpToPlayerAbove();

        float dir = Mathf.Sign(distanceX);
        SetHorizontalVelocity(dir * moveSpeed);
        lastMoveDir = dir;
        FlipByVelocity(dir);
    }

    // GOAP charge actions call this for a fixed windup -> burst -> recovery cycle.
    // Direction is locked when the burst starts, so dodging after windup matters.
    public void ChargePlayer()
    {
        ResetPatrolPause();

        if (state != State.Dead)
            state = State.Charge;

        if (player == null)
        {
            ResetChargeCycle();
            state = State.Patrol;
            return;
        }

        if (chargePhase == ChargePhase.None)
        {
            if (!chargeCycleStartAllowed)
            {
                SetHorizontalVelocity(0f);
                SetChargeVisuals(false, false);
                return;
            }

            if (Time.time < nextChargeAllowedTime)
            {
                SetHorizontalVelocity(0f);
                SetChargeVisuals(false, false);
                return;
            }

            BeginChargeWindup();
        }

        switch (chargePhase)
        {
            case ChargePhase.Windup:
                UpdateChargeWindup();
                break;

            case ChargePhase.Charging:
                UpdateFixedCharge();
                break;

            case ChargePhase.Recovery:
                UpdateChargeRecovery();
                break;
        }
    }

    // GOAP action will call StopMoving() later.
    public void StopMoving()
    {
        ResetPatrolPause();
        SetHorizontalVelocity(0f);
    }

    public void BeginPatrolAnimationPause()
    {
        if (!usePatrolAnimationEvents || state != State.Patrol || IsDead)
        {
            return;
        }

        ResetPatrolMovePause();
        isPatrolAnimationPaused = true;
        isPatrolMovePaused = true;
        patrolAnimationPauseFallbackTime = Time.time + Mathf.Max(0f, maxPatrolAnimationPauseSeconds);
    }

    public void EndPatrolAnimationPause()
    {
        ResetPatrolAnimationPause();
    }

    public void CancelChargeCycle()
    {
        ResetChargeCycle();
    }

    public void SetChargeCycleStartAllowed(bool allowed)
    {
        chargeCycleStartAllowed = allowed;
    }

    public void SetChargeVisuals(bool chargeStarting, bool charging)
    {
        if (animationDriver == null)
        {
            return;
        }

        animationDriver.SetChargeStarting(chargeStarting);
        animationDriver.SetCharging(charging);
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
        bool grounded = IsGrounded();

        if (!IsPlayerAbove())
        {
            playerAboveDetectedSince = -1f;
            if (grounded)
            {
                playerAboveJumpConsumed = false;
            }
            return false;
        }

        if (playerAboveJumpConsumed)
        {
            return false;
        }

        if (!grounded)
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

    public bool TryJumpToPlayerAbove()
    {
        if (!IsPlayerAboveReadyToJump())
        {
            return false;
        }

        if (!TryJump())
        {
            return false;
        }

        playerAboveJumpConsumed = true;
        playerAboveDetectedSince = -1f;
        return true;
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

    private void BeginChargeWindup()
    {
        chargePhase = ChargePhase.Windup;
        chargePhaseEndTime = Time.time + Mathf.Max(0f, chargeStartDuration);
        SetHorizontalVelocity(0f);
        SetChargeVisuals(true, false);
        FaceTarget(player);
    }

    private void UpdateChargeWindup()
    {
        SetHorizontalVelocity(0f);
        SetChargeVisuals(true, false);

        if (Time.time < chargePhaseEndTime)
        {
            return;
        }

        BeginFixedCharge();
    }

    private void BeginFixedCharge()
    {
        float dx = player != null ? player.position.x - transform.position.x : lastMoveDir;
        if (Mathf.Abs(dx) > 0.001f)
        {
            chargeDirection = Mathf.Sign(dx);
        }
        else
        {
            chargeDirection = Mathf.Abs(lastMoveDir) < 0.001f ? 1f : Mathf.Sign(lastMoveDir);
        }

        chargeStartX = transform.position.x;
        chargePhase = ChargePhase.Charging;
        chargePhaseEndTime = Time.time + Mathf.Max(0.01f, chargeDuration);
        SetChargeVisuals(false, true);
        FlipByVelocity(chargeDirection);
    }

    private void UpdateFixedCharge()
    {
        SetChargeVisuals(false, true);

        float speed = Mathf.Max(chargeSpeed, moveSpeed);
        SetHorizontalVelocity(chargeDirection * speed);
        lastMoveDir = chargeDirection;
        FlipByVelocity(chargeDirection);

        bool durationComplete = Time.time >= chargePhaseEndTime;
        bool distanceComplete = chargeDistance > 0f && Mathf.Abs(transform.position.x - chargeStartX) >= chargeDistance;
        if (durationComplete || distanceComplete)
        {
            BeginChargeRecovery();
        }
    }

    private void BeginChargeRecovery()
    {
        chargePhase = ChargePhase.Recovery;
        chargePhaseEndTime = Time.time + Mathf.Max(0f, chargeRecoveryDuration);
        SetHorizontalVelocity(0f);
        SetChargeVisuals(false, false);
    }

    private void UpdateChargeRecovery()
    {
        SetHorizontalVelocity(0f);
        SetChargeVisuals(false, false);

        if (Time.time >= chargePhaseEndTime)
        {
            chargePhase = ChargePhase.None;
            nextChargeAllowedTime = Time.time + Mathf.Max(0f, chargeCooldownDuration);
        }
    }

    private void ResetChargeCycle()
    {
        if (chargePhase == ChargePhase.None)
        {
            SetChargeVisuals(false, false);
            return;
        }

        chargePhase = ChargePhase.None;
        chargePhaseEndTime = 0f;
        nextChargeAllowedTime = 0f;
        SetChargeVisuals(false, false);
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
        if (animationDriver == null)
        {
            return;
        }

        float speedAbs = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
        if (isPatrolMovePaused && state == State.Patrol)
        {
            speedAbs = Mathf.Max(speedAbs, moveSpeed);
        }

        animationDriver.SetMovement(speedAbs);
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

        ResetChargeCycle();
        ResetPatrolPause();
        state = State.Dead;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        RestoreMovementPhysicsMaterial();

        ContactDamage[] damageSources = GetComponentsInChildren<ContactDamage>(true);
        for (int i = 0; i < damageSources.Length; i++)
        {
            damageSources[i].enabled = false;
        }

        // Leave the corpse active. Enemy.cs ensures DrainableCorpse exists/enabled on death.
        enabled = false;
    }
}
