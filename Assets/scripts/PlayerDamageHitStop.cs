using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class PlayerDamageHitStop : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Health health;

    [Header("Hit Stop")]
    [SerializeField] private float duration = 0.06f;
    [SerializeField] private float stoppedTimeScale = 0.05f;

    private Coroutine hitStopRoutine;
    private bool hasStoredTimeScale;
    private float storedTimeScale = 1f;

    private void Reset()
    {
        health = GetComponent<Health>();
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }

        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
        }

        RestoreTimeScale();
    }

    private void HandleDamaged(int damage)
    {
        if (duration <= 0f || Time.timeScale <= 0f)
        {
            return;
        }

        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            RestoreTimeScale();
        }

        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        storedTimeScale = Time.timeScale;
        hasStoredTimeScale = true;

        Time.timeScale = Mathf.Clamp(stoppedTimeScale, 0f, storedTimeScale);
        yield return new WaitForSecondsRealtime(duration);

        RestoreTimeScale();
        hitStopRoutine = null;
    }

    private void RestoreTimeScale()
    {
        if (!hasStoredTimeScale)
        {
            return;
        }

        Time.timeScale = storedTimeScale;
        hasStoredTimeScale = false;
    }
}
