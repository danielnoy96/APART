using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackReceiver : MonoBehaviour
{
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
    }

    public void ApplyKnockback(Vector2 sourcePosition)
    {
        if (rb == null)
        {
            return;
        }

        knockbackEndTime = Time.time + Mathf.Max(0f, knockbackDuration);

        Vector2 away = (Vector2)transform.position - sourcePosition;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector2.right;
        }
        away.Normalize();

        float x = away.x;
        if (Mathf.Abs(x) < 0.001f)
        {
            // If the hit is perfectly vertical, pick a reasonable side.
            x = Mathf.Abs(rb.linearVelocity.x) > 0.01f ? Mathf.Sign(rb.linearVelocity.x) : 1f;
        }

        // Guarantee some horizontal push so we separate from the source, even for bottom hits.
        float horizontal = Mathf.Sign(x) * Mathf.Max(Mathf.Abs(away.x), minHorizontalFactor);
        float vertical = Mathf.Max(away.y, 0f);

        Vector2 velocity = rb.linearVelocity;
        velocity.x = horizontal * knockbackForce;
        velocity.y = Mathf.Max(velocity.y, 0f) + (vertical * knockbackUpwardForce);
        rb.linearVelocity = velocity;
    }
}
