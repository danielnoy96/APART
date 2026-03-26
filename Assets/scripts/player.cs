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
    private bool jumpPressed;
    private bool jumpReleased;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    private bool isGrounded;


    private void Start()
    {
        rb.gravityScale = normalGravity;
    }

    void FixedUpdate()
    {
        ApplyVariableGravity();
        CheckGrounded();
        HandleMovement();
        HandleJump();
    }


    private void HandleMovement()
    {
        float targetSpeed = moveInput.x * speed;
        rb.linearVelocity = new Vector2(1 * targetSpeed, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
       if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpPressed = false;
            jumpReleased = false;
        }
        if (jumpReleased)
        {
            if (rb.linearVelocity.y > 0) //if still going up
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            }
                jumpReleased = false;
        }
    }


    void Update()
    {
        Flip();
    }


    void ApplyVariableGravity()
    {
        if (rb.linearVelocity.y < -0.1f) //falling
        {
            rb.gravityScale = fallGravity;
        }
        else if (rb.linearVelocity.y > 0.1f ) //rising 
        {
            rb.gravityScale = jumpGravity;
        }
        else //normal gravity
        {
            rb.gravityScale = normalGravity;
        }
    }


    void CheckGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
            jumpPressed = true;
            jumpReleased = false;
        }
        else //button is released
        {
            jumpReleased = true;
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
