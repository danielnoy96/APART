using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class PlayerDamageCameraShake : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Health health;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Shake")]
    [SerializeField] private float force = 1f;
    [SerializeField] private bool scaleWithDamage = false;
    [SerializeField] private float damageForceMultiplier = 0.25f;

    private void Reset()
    {
        health = GetComponent<Health>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
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
    }

    private void HandleDamaged(int damage)
    {
        if (impulseSource == null)
        {
            return;
        }

        float shakeForce = force;
        if (scaleWithDamage)
        {
            shakeForce += Mathf.Max(0, damage - 1) * damageForceMultiplier;
        }

        impulseSource.GenerateImpulseWithForce(shakeForce);
    }
}
