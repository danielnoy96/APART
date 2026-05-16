using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRespawnController : MonoBehaviour
{
    [SerializeField] private player player;
    [SerializeField] private Health health;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<player>();
        }

        if (player != null)
        {
            if (health == null)
            {
                health = player.health != null ? player.health : player.GetComponentInChildren<Health>();
            }

            CheckpointManager.Instance.RegisterDefaultSpawn(player.transform.position);
        }
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

    private void HandleDeath()
    {
        if (player == null)
        {
            return;
        }

        CheckpointManager.Instance.RespawnToPermanent(player);
    }
}

