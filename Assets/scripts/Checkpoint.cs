using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public enum CheckpointType
    {
        Permanent,
        Mini
    }

    [SerializeField] private CheckpointType checkpointType = CheckpointType.Permanent;
    [Tooltip("Optional override point to respawn at (e.g., a child marker on a bench). If null, uses this transform.")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private bool logActivation = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TryActivate(collision.collider);
    }

    private void TryActivate(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        player p = other.GetComponentInParent<player>();
        if (p == null)
        {
            return;
        }

        Transform point = respawnPoint != null ? respawnPoint : transform;

        if (checkpointType == CheckpointType.Permanent)
        {
            CheckpointManager.Instance.SetPermanent(point);
        }
        else
        {
            CheckpointManager.Instance.SetMini(point);
        }

        if (logActivation)
        {
            Debug.Log($"[Checkpoint] Activated {checkpointType} -> {point.name} ({point.position})", this);
        }
    }
}
