using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody2D rb;
    public PlayerInput playerInput;
    public Animator anim;

    [Header("FSM (Runtime)")]
    [HideInInspector] public PlayerState currentState;
    [HideInInspector] public PlayerIdleState idleState;
    [HideInInspector] public PlayerMoveState moveState;
    [HideInInspector] public PlayerJumpState jumpState;

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

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    private bool isGrounded;

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        idleState = new PlayerIdleState(this);
        moveState = new PlayerMoveState(this);
        jumpState = new PlayerJumpState(this);

        ChangeState(idleState);
    }

    private void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        rb.gravityScale = normalGravity;
    }

    void FixedUpdate()
    {
        CheckGrounded();
        currentState.FixedUpdate();
    }

    void Update()
    {
        currentState.Update();
    }

    void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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

    public void ChangeState(PlayerState newState)
    {
        if (newState == null)
        {
            return;
        }

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
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


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
