using UnityEngine;
using UnityEngine.InputSystem;

public class StaminaDebug : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Stamina stamina;

    [Header("Controls")]
    [SerializeField] private Key spendKey = Key.P;
    [SerializeField] private Key restoreKey = Key.O;
    [SerializeField] private Key restoreFullKey = Key.I;
    [SerializeField] private float amount = 10f;

    [Header("Logging")]
    [SerializeField] private bool logChanged = true;
    [SerializeField] private bool logEmptyFull = true;

    private void Awake()
    {
        if (stamina == null)
        {
            stamina = GetComponent<Stamina>();
        }
        if (stamina == null)
        {
            stamina = GetComponentInChildren<Stamina>();
        }
    }

    private void OnEnable()
    {
        if (stamina == null)
        {
            Debug.LogWarning("StaminaDebug: No Stamina reference found. Assign it or place Stamina on the same GameObject/child.", this);
            return;
        }

        stamina.OnStaminaChanged += HandleChanged;
        stamina.OnStaminaEmpty += HandleEmpty;
        stamina.OnStaminaFull += HandleFull;
    }

    private void OnDisable()
    {
        if (stamina == null)
        {
            return;
        }

        stamina.OnStaminaChanged -= HandleChanged;
        stamina.OnStaminaEmpty -= HandleEmpty;
        stamina.OnStaminaFull -= HandleFull;
    }

    private void Update()
    {
        if (stamina == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard[spendKey].wasPressedThisFrame)
        {
            bool spent = stamina.TrySpend(amount);
            Debug.Log(spent
                ? $"StaminaDebug: Spent {amount} (now {stamina.CurrentStamina:0.##}/{stamina.MaxStamina:0.##})"
                : $"StaminaDebug: Not enough stamina to spend {amount} (now {stamina.CurrentStamina:0.##}/{stamina.MaxStamina:0.##})",
                this);
        }

        if (keyboard[restoreKey].wasPressedThisFrame)
        {
            stamina.Restore(amount);
            Debug.Log($"StaminaDebug: Restored {amount} (now {stamina.CurrentStamina:0.##}/{stamina.MaxStamina:0.##})", this);
        }

        if (keyboard[restoreFullKey].wasPressedThisFrame)
        {
            stamina.RestoreFull();
            Debug.Log($"StaminaDebug: Restored full (now {stamina.CurrentStamina:0.##}/{stamina.MaxStamina:0.##})", this);
        }
    }

    private void HandleChanged(float current, float max)
    {
        if (!logChanged)
        {
            return;
        }

        Debug.Log($"StaminaDebug: Changed {current:0.##}/{max:0.##}", this);
    }

    private void HandleEmpty()
    {
        if (!logEmptyFull)
        {
            return;
        }

        Debug.Log("StaminaDebug: Empty", this);
    }

    private void HandleFull()
    {
        if (!logEmptyFull)
        {
            return;
        }

        Debug.Log("StaminaDebug: Full", this);
    }
}
