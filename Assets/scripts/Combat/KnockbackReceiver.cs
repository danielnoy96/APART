using UnityEngine;

public class KnockbackReceiver : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logKnockback = false;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackUpwardForce = 4f;
    [SerializeField] private float knockbackDuration = 0.12f;
    [Tooltip("Ensures some horizontal separation even when the hit direction is mostly vertical (e.g., landing on an enemy).")]
    [Range(0f, 1f)]
    [SerializeField] private float minHorizontalFactor = 0.35f;

    private Rigidbody2D rb;
    private float knockbackEndTime;

    public float KnockbackDuration => knockbackDuration;
    public bool IsKnockbackActive => Time.time < knockbackEndTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            // Common setup: KnockbackReceiver sits on a child, while the Rigidbody2D is on the root.
            rb = GetComponentInParent<Rigidbody2D>();
        }
    }

    public void ApplyKnockback(Vector2 sourcePosition)
    {
        if (rb == null)
        {
            return;
        }

        knockbackEndTime = Time.time + Mathf.Max(0f, knockbackDuration);

        // Use the Rigidbody's center to determine direction (the receiver may be on a child).
        Vector2 center = rb.worldCenterOfMass;
        Vector2 away = center - sourcePosition;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector2.right;
        }
        away.Normalize();

        // Choose horizontal direction based on relative positions (not current velocity).
        float dx = center.x - sourcePosition.x;
        float xSign = Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : (away.x >= 0f ? 1f : -1f);

        // Guarantee some horizontal push so we separate from the source, even for bottom hits.
        float horizontal = xSign * Mathf.Max(Mathf.Abs(away.x), minHorizontalFactor);
        float vertical = Mathf.Max(away.y, 0f);

        Vector2 velocity = rb.linearVelocity;
        if (logKnockback)
        {
            Debug.Log($"KnockbackReceiver({gameObject.name} -> RB:{rb.gameObject.name}) before v={velocity} source={sourcePosition} center={center}", this);
        }
        velocity.x = horizontal * knockbackForce;
        // Side hits should be mostly knockback with a small pop; bottom hits pop more.
        float upward = knockbackUpwardForce * (0.35f + (0.65f * vertical));
        velocity.y = Mathf.Max(velocity.y, 0f) + upward;
        rb.linearVelocity = velocity;

        if (logKnockback)
        {
            Debug.Log($"KnockbackReceiver({gameObject.name} -> RB:{rb.gameObject.name}) after v={rb.linearVelocity} h={horizontal} f={knockbackForce} up={upward} dur={knockbackDuration}", this);
        }
    }
}
