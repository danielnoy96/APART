using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    public Rigidbody2D rb;
    public PlayerInput playerInput;

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

    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool jumpHeld;
    private bool jumpCutQueued;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    private bool isGrounded;


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
        UpdateJumpTimers();
        HandleMovement();
        HandleJump();
        ApplyVariableGravity();
        ClampVerticalSpeed();
    }


    private void HandleMovement()
    {
        float targetSpeed = moveInput.x * speed;
        rb.linearVelocity = new Vector2(1 * targetSpeed, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            PerformJump();
        }

        if (jumpCutQueued)
        {
            if (rb.linearVelocity.y > 0f) // still going up
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            }
            jumpCutQueued = false;
        }
    }


    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpCutQueued = false;
    }


    void Update()
    {
        Flip();
    }


    void ApplyVariableGravity()
    {
        float gravityScale;
        if (rb.linearVelocity.y < -0.1f) //falling
        {
            gravityScale = fallGravity;
        }
        else if (rb.linearVelocity.y > 0.1f) //rising 
        {
            gravityScale = jumpGravity;
        }
        else //normal gravity
        {
            gravityScale = normalGravity;
        }

        // Celeste-style subtle apex hang: while holding jump near the top, apply reduced gravity.
        if (!isGrounded && jumpHeld && apexHangVelocityThreshold > 0f && Mathf.Abs(rb.linearVelocity.y) <= apexHangVelocityThreshold)
        {
            gravityScale *= apexHangGravityMultiplier;
        }

        rb.gravityScale = gravityScale;
    }


    void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }


    private void UpdateJumpTimers()
    {
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        }

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.fixedDeltaTime);
    }


    private void ClampVerticalSpeed()
    {
        if (maxFallSpeed <= 0f && maxRiseSpeed <= 0f)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        if (maxFallSpeed > 0f)
        {
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }
        if (maxRiseSpeed > 0f)
        {
            velocity.y = Mathf.Min(velocity.y, maxRiseSpeed);
        }
        rb.linearVelocity = velocity;
    }


    void Flip()
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


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
