using UnityEngine;

public class InstantKillHazard : MonoBehaviour
{
    [Tooltip("If true, the hazard won't trigger while the player is in respawn grace time (prevents immediate re-trigger loops).")]
    [SerializeField] private bool respectRespawnGrace = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (respectRespawnGrace && CheckpointManager.Instance.IsRespawnGraceActive)
        {
            return;
        }

        player p = other.GetComponentInParent<player>();
        if (p == null)
        {
            return;
        }

        CheckpointManager.Instance.RespawnToMini(p);
    }
}

