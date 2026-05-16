using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    private static CheckpointManager instance;

    [Header("Respawn")]
    [SerializeField] private float respawnGraceSeconds = 0.25f;
    [SerializeField] private bool logEvents = false;

    private Transform permanentCheckpoint;
    private Transform miniCheckpoint;
    private Vector3 defaultSpawnPosition;
    private bool hasDefaultSpawn;
    private float respawnGraceUntilTime;

    public static CheckpointManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<CheckpointManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject go = new GameObject("CheckpointManager");
            instance = go.AddComponent<CheckpointManager>();
            return instance;
        }
    }

    public bool IsRespawnGraceActive => Time.time < respawnGraceUntilTime;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterDefaultSpawn(Vector3 spawnPosition)
    {
        defaultSpawnPosition = spawnPosition;
        hasDefaultSpawn = true;
    }

    public void SetPermanent(Transform point)
    {
        if (point == null)
        {
            return;
        }

        permanentCheckpoint = point;
        if (logEvents)
        {
            Debug.Log($"[Checkpoint] Permanent set -> {point.name} ({point.position})", this);
        }
    }

    public void SetMini(Transform point)
    {
        if (point == null)
        {
            return;
        }

        miniCheckpoint = point;
        if (logEvents)
        {
            Debug.Log($"[Checkpoint] Mini set -> {point.name} ({point.position})", this);
        }
    }

    public void RespawnToPermanent(player p)
    {
        if (p == null)
        {
            return;
        }

        Vector3 pos = permanentCheckpoint != null ? permanentCheckpoint.position : GetFallbackSpawn(p);
        if (logEvents)
        {
            string src = permanentCheckpoint != null ? $"permanent ({permanentCheckpoint.name})" : "fallback (default spawn / current pos)";
            Debug.Log($"[Checkpoint] RespawnToPermanent -> {src} ({pos})", this);
        }
        DoRespawn(p, pos, restoreHealthFull: true, restoreStaminaFull: true);
    }

    public void RespawnToMini(player p)
    {
        if (p == null)
        {
            return;
        }

        Vector3 pos = miniCheckpoint != null ? miniCheckpoint.position : GetFallbackSpawn(p);
        if (logEvents)
        {
            string src = miniCheckpoint != null ? $"mini ({miniCheckpoint.name})" : "fallback (default spawn / current pos)";
            Debug.Log($"[Checkpoint] RespawnToMini -> {src} ({pos})", this);
        }
        DoRespawn(p, pos, restoreHealthFull: false, restoreStaminaFull: false);
    }

    private Vector3 GetFallbackSpawn(player p)
    {
        if (hasDefaultSpawn)
        {
            return defaultSpawnPosition;
        }

        return p.transform.position;
    }

    private void DoRespawn(player p, Vector3 position, bool restoreHealthFull, bool restoreStaminaFull)
    {
        respawnGraceUntilTime = Time.time + Mathf.Max(0f, respawnGraceSeconds);

        p.ResetForRespawn(position);

        if (restoreHealthFull && p.health != null)
        {
            p.health.ReviveFull();
        }

        if (restoreStaminaFull && p.stamina != null)
        {
            p.stamina.RestoreFull();
        }
    }
}
