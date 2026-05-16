using UnityEngine;

public class InstantKillHazard : MonoBehaviour
{
    [Tooltip("If true, the hazard won't trigger while the player is in respawn grace time (prevents immediate re-trigger loops).")]
    [SerializeField] private bool respectRespawnGrace = true;
    [Header("Damage")]
    [Tooltip("Damage applied when the player touches the hazard. If 0, no damage is applied.")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("If true, always respawns to the mini checkpoint after applying damage.")]
    [SerializeField] private bool respawnAfterDamage = true;

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

        if (damageAmount > 0)
        {
            p.TryTakeDamage(damageAmount);
        }

        if (respawnAfterDamage)
        {
            CheckpointManager.Instance.RespawnToMini(p);
        }
    }
}
