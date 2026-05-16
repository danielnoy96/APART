using System.Collections;
using UnityEngine;

public class BreakableTimedPlatform : MonoBehaviour
{
    [SerializeField] private float breakDelay = 0.9f;
    [SerializeField] private float respawnDelay = 0.9f;
    [SerializeField] private LayerMask triggerLayer;

    private Collider2D[] colliders;
    private Renderer[] renderers;
    private bool busy;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Reset()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            triggerLayer = 1 << playerLayer;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrigger(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTrigger(collision.collider);
    }

    private void TryTrigger(Collider2D other)
    {
        if (busy || other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & triggerLayer.value) == 0)
        {
            return;
        }

        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        busy = true;

        yield return new WaitForSeconds(breakDelay);
        SetEnabled(false);

        yield return new WaitForSeconds(respawnDelay);
        SetEnabled(true);

        busy = false;
    }

    private void SetEnabled(bool enabled)
    {
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                {
                    c.enabled = enabled;
                }
            }
        }

        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.enabled = enabled;
                }
            }
        }
    }
}

