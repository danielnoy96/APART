using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(player))]
public class PlayerCombo : MonoBehaviour
{
    [Header("Combo")]
    [Tooltip("How many successful hits are required to complete a combo.")]
    [SerializeField] private int comboHitCount = 3;
    [Tooltip("Seconds allowed between successful hits before the combo resets.")]
    [SerializeField] private float comboResetSeconds = 1.0f;
    [Tooltip("Stamina restored when completing the full combo.")]
    [SerializeField] private float comboStaminaRefund = 15f;
    [Tooltip("Logs combo progress to the Console (debug).")]
    [SerializeField] private bool logCombo = false;

    private player player;
    private Combat combat;
    private Stamina stamina;

    private int currentComboHits;
    private float comboExpiresAtTime;

    public int RequiredHits => Mathf.Max(1, comboHitCount);
    public int CurrentHits => currentComboHits;

    private void Awake()
    {
        player = GetComponent<player>();
        combat = player != null ? player.combat : null;
        stamina = player != null ? player.stamina : null;
    }

    private void OnEnable()
    {
        if (combat != null)
        {
            combat.OnHitCheckCompleted += OnPlayerHitCheckCompleted;
        }
    }

    private void OnDisable()
    {
        if (combat != null)
        {
            combat.OnHitCheckCompleted -= OnPlayerHitCheckCompleted;
        }
    }

    private void Update()
    {
        UpdateComboTimeout();
    }

    public void ResetCombo()
    {
        currentComboHits = 0;
        comboExpiresAtTime = 0f;
    }

    private void UpdateComboTimeout()
    {
        if (currentComboHits <= 0)
        {
            return;
        }

        if (comboResetSeconds <= 0f)
        {
            return;
        }

        if (Time.time > comboExpiresAtTime)
        {
            ResetCombo();
        }
    }

    private void OnPlayerHitCheckCompleted(bool hitSomething)
    {
        if (!hitSomething)
        {
            return;
        }

        int requiredHits = RequiredHits;

        if (currentComboHits > 0 && comboResetSeconds > 0f && Time.time > comboExpiresAtTime)
        {
            currentComboHits = 0;
        }

        currentComboHits = Mathf.Clamp(currentComboHits + 1, 0, requiredHits);
        comboExpiresAtTime = Time.time + Mathf.Max(0f, comboResetSeconds);

        if (logCombo)
        {
            Debug.Log($"Combo: {currentComboHits}/{requiredHits}", this);
        }

        if (currentComboHits >= requiredHits)
        {
            TryRefundStamina();
            ResetCombo();
        }
    }

    private void TryRefundStamina()
    {
        if (stamina == null || comboStaminaRefund <= 0f)
        {
            return;
        }

        stamina.Restore(comboStaminaRefund);

        if (logCombo)
        {
            Debug.Log($"Combo complete: +{comboStaminaRefund} stamina", this);
        }
    }
}
