using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private player player;
    [SerializeField] private Health health;
    [SerializeField] private Stamina stamina;

    [Header("Health Bar (assign one)")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Slider healthSlider;

    [Header("Stamina Bar (assign one)")]
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Slider staminaSlider;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<player>();
        }

        if (player != null)
        {
            if (health == null)
            {
                health = player.health != null ? player.health : player.GetComponentInChildren<Health>();
            }

            if (stamina == null)
            {
                stamina = player.stamina != null ? player.stamina : player.GetComponentInChildren<Stamina>();
            }
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged += HandleHealthChanged;
            health.OnHealed += HandleHealthChanged;
            health.OnDeath += HandleDeath;
        }

        if (stamina != null)
        {
            stamina.OnStaminaChanged += HandleStaminaChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleHealthChanged;
            health.OnHealed -= HandleHealthChanged;
            health.OnDeath -= HandleDeath;
        }

        if (stamina != null)
        {
            stamina.OnStaminaChanged -= HandleStaminaChanged;
        }
    }

    private void HandleHealthChanged(int _)
    {
        RefreshHealth();
    }

    private void HandleDeath()
    {
        RefreshHealth();
    }

    private void HandleStaminaChanged(float current, float max)
    {
        SetBar(staminaFillImage, staminaSlider, current, max);
    }

    private void RefreshAll()
    {
        RefreshHealth();
        RefreshStamina();
    }

    private void RefreshHealth()
    {
        if (health == null)
        {
            return;
        }

        SetBar(healthFillImage, healthSlider, health.CurrentHealth, health.MaxHealth);
    }

    private void RefreshStamina()
    {
        if (stamina == null)
        {
            return;
        }

        SetBar(staminaFillImage, staminaSlider, stamina.CurrentStamina, stamina.MaxStamina);
    }

    private static void SetBar(Image fillImage, Slider slider, float current, float max)
    {
        float value01 = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = value01;
        }

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = max;
            slider.value = current;
        }
    }
}
