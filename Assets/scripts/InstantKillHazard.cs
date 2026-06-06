using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstantKillHazard : MonoBehaviour
{
    [Tooltip("If true, the hazard won't trigger while the player is in respawn grace time (prevents immediate re-trigger loops).")]
    [SerializeField] private bool respectRespawnGrace = true;
    [Tooltip("Fallback respawn delay when no damage/hurt animation plays.")]
    [SerializeField] private float triggerDelaySeconds = 0.5f;
    [Header("Damage")]
    [Tooltip("Damage applied when the player touches the hazard. If 0, no damage is applied.")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("If true, always respawns to the mini checkpoint after applying damage.")]
    [SerializeField] private bool respawnAfterDamage = true;

    private readonly HashSet<player> pendingPlayers = new HashSet<player>();
    private readonly Dictionary<player, FrozenPlayerState> frozenPlayers = new Dictionary<player, FrozenPlayerState>();

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreAllFrozenPlayers();
        pendingPlayers.Clear();
    }

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

        if (!pendingPlayers.Add(p))
        {
            return;
        }

        StartCoroutine(TriggerHazard(p));
    }

    private IEnumerator TriggerHazard(player p)
    {
        if (p == null)
        {
            pendingPlayers.Remove(p);
            yield break;
        }

        if (respectRespawnGrace && CheckpointManager.Instance.IsRespawnGraceActive)
        {
            pendingPlayers.Remove(p);
            yield break;
        }

        if (respawnAfterDamage)
        {
            CheckpointManager.Instance.BeginRespawnGrace();
        }

        bool killedPlayer = false;
        bool damageAccepted = false;
        if (damageAmount > 0)
        {
            Health playerHealth = p.health;
            int healthBeforeDamage = playerHealth != null ? playerHealth.CurrentHealth : int.MaxValue;
            damageAccepted = p.TryTakeDamage(damageAmount);
            killedPlayer = damageAccepted && playerHealth != null && healthBeforeDamage > 0 && playerHealth.IsDead;
        }

        FreezePlayer(p);

        float delay = damageAccepted ? Mathf.Max(0f, p.hitAnimHoldSeconds) : Mathf.Max(0f, triggerDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        pendingPlayers.Remove(p);
        RestorePlayer(p);

        if (!respawnAfterDamage || p == null)
        {
            yield break;
        }

        if (killedPlayer)
        {
            CheckpointManager.Instance.RespawnToPermanent(p);
        }
        else
        {
            CheckpointManager.Instance.RespawnToMini(p);
        }
    }

    private void FreezePlayer(player p)
    {
        if (p == null || frozenPlayers.ContainsKey(p))
        {
            return;
        }

        Rigidbody2D rb = p.rb != null ? p.rb : p.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            return;
        }

        frozenPlayers[p] = new FrozenPlayerState(rb, rb.gravityScale, rb.constraints);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.constraints |= RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
    }

    private void RestorePlayer(player p)
    {
        if (p == null || !frozenPlayers.TryGetValue(p, out FrozenPlayerState state))
        {
            return;
        }

        frozenPlayers.Remove(p);

        if (state.Rigidbody == null)
        {
            return;
        }

        state.Rigidbody.constraints = state.Constraints;
        state.Rigidbody.gravityScale = state.GravityScale;
        state.Rigidbody.linearVelocity = Vector2.zero;
        state.Rigidbody.angularVelocity = 0f;
    }

    private void RestoreAllFrozenPlayers()
    {
        foreach (FrozenPlayerState state in frozenPlayers.Values)
        {
            if (state.Rigidbody == null)
            {
                continue;
            }

            state.Rigidbody.constraints = state.Constraints;
            state.Rigidbody.gravityScale = state.GravityScale;
            state.Rigidbody.linearVelocity = Vector2.zero;
            state.Rigidbody.angularVelocity = 0f;
        }

        frozenPlayers.Clear();
    }

    private readonly struct FrozenPlayerState
    {
        public FrozenPlayerState(Rigidbody2D rigidbody, float gravityScale, RigidbodyConstraints2D constraints)
        {
            Rigidbody = rigidbody;
            GravityScale = gravityScale;
            Constraints = constraints;
        }

        public readonly Rigidbody2D Rigidbody;
        public readonly float GravityScale;
        public readonly RigidbodyConstraints2D Constraints;
    }
}
