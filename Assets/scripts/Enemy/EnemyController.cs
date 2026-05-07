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

    private State state;
    private Vector2 spawnPosition;
    private int patrolIndex;
    private int patrolDirection = 1;
    private float idleUntilTime;
    private Collider2D contactDamageSensor;
    private Collider2D playerCollider;
    private Transform contactDamageSensorTransform;
    private Vector3 contactSensorInitialLocalPos;
    private bool contactSensorHasInitial;

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

        spawnPosition = transform.position;
        state = State.Patrol;
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
        FlipByVelocity(Mathf.Sign(delta));
    }

    // GOAP action will call ChasePlayer() later.
    public void ChasePlayer()
    {
        if (player == null)
        {
            state = State.Patrol;
            return;
        }

        float distanceX = player.position.x - transform.position.x;

        if (ShouldStopChasing(distanceX))
        {
            SetHorizontalVelocity(0f);
            return;
        }

        float dir = Mathf.Sign(distanceX);
        SetHorizontalVelocity(dir * moveSpeed);
        FlipByVelocity(dir);
    }

    // GOAP action will call StopMoving() later.
    public void StopMoving()
    {
        SetHorizontalVelocity(0f);
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
