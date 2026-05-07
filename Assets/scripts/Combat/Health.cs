using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int currentHealth = 10;

    public event Action<int> OnDamaged;
    public event Action<int> OnHealed;
    public event Action OnDeath;

    private bool isDead;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

        int appliedDamage = previousHealth - currentHealth;
        if (appliedDamage > 0)
        {
            OnDamaged?.Invoke(appliedDamage);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);

        int appliedHeal = currentHealth - previousHealth;
        if (appliedHeal > 0)
        {
            OnHealed?.Invoke(appliedHeal);
        }
    }

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0;
        OnDeath?.Invoke();
    }
}

