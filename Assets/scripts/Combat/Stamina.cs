using System;
using UnityEngine;

public class Stamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;

    [Header("Regeneration")]
    [Tooltip("Stamina per second regenerated after the delay has passed.")]
    [SerializeField] private float staminaRegenRate = 25f;
    [Tooltip("Seconds after spending stamina before regen begins.")]
    [SerializeField] private float regenDelayAfterSpend = 0.5f;

    public event Action<float, float> OnStaminaChanged;
    public event Action OnStaminaEmpty;
    public event Action OnStaminaFull;

    private float regenBlockedUntilTime;
    private bool emptyFired;
    private bool fullFired;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    private void Awake()
    {
        maxStamina = Mathf.Max(0f, maxStamina);
        currentStamina = maxStamina;

        EvaluateEdgeEvents(force: true);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void Update()
    {
        if (maxStamina <= 0f)
        {
            return;
        }

        if (Time.time < regenBlockedUntilTime)
        {
            return;
        }

        if (currentStamina >= maxStamina || staminaRegenRate <= 0f)
        {
            return;
        }

        float previous = currentStamina;
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);

        if (!Mathf.Approximately(previous, currentStamina))
        {
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            EvaluateEdgeEvents(force: false);
        }
    }

    public bool HasStamina(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        return currentStamina >= amount;
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (!HasStamina(amount))
        {
            return false;
        }

        SetStamina(currentStamina - amount);
        regenBlockedUntilTime = Time.time + Mathf.Max(0f, regenDelayAfterSpend);
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetStamina(currentStamina + amount);
    }

    public void RestoreFull()
    {
        SetStamina(maxStamina);
    }

    private void SetStamina(float newValue)
    {
        float previous = currentStamina;
        currentStamina = Mathf.Clamp(newValue, 0f, maxStamina);

        if (Mathf.Approximately(previous, currentStamina))
        {
            return;
        }

        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        EvaluateEdgeEvents(force: false);
    }

    private void EvaluateEdgeEvents(bool force)
    {
        bool isEmptyNow = maxStamina > 0f && currentStamina <= 0f;
        bool isFullNow = maxStamina > 0f && currentStamina >= maxStamina;

        if ((force || !emptyFired) && isEmptyNow)
        {
            emptyFired = true;
            OnStaminaEmpty?.Invoke();
        }
        else if (!isEmptyNow)
        {
            emptyFired = false;
        }

        if ((force || !fullFired) && isFullNow)
        {
            fullFired = true;
            OnStaminaFull?.Invoke();
        }
        else if (!isFullNow)
        {
            fullFired = false;
        }
    }
}
