using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;
using System;

public class TapePiece : MonoBehaviour
{
    [SerializeField] private int health = 3;
    private int maxHealth;

    [Header("Rope Pieces")]
    [SerializeField] private Transform[] ropePieces; // 🔹 два объекта верёвки

    [Header("Scale Settings")]
    [SerializeField] private float minScaleY = 0.3f;        // 🔹 scale при здоровье = 0
    [SerializeField] private float maxScaleY = 1f;          // 🔹 scale при здоровье = максимум
    [SerializeField] private float scaleTweenDuration = 0.3f; // 🔹 время плавного изменения

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeStrength = 0.3f;
    [SerializeField] private int shakeVibrato = 20;
    [SerializeField] private float shakeRandomness = 90f;

    [Header("Damage Settings")]
    [SerializeField] private float damageCooldown = 0.3f;

    private Tween shakeTween;
    private float lastDamageTime = -999f;
    private Vector3[] originalScales;

    public event Action<TapePiece, int> HealthChanged;
    public int Health => health;
    public int MaxHealth => maxHealth > 0 ? maxHealth : health;

    void Start()
    {
        maxHealth = health;

        // 🔹 запоминаем исходные размеры каждой верёвки
        originalScales = new Vector3[ropePieces.Length];
        for (int i = 0; i < ropePieces.Length; i++)
        {
            if (ropePieces[i] != null)
                originalScales[i] = ropePieces[i].localScale;
        }
    }

    public void TakeDamage(int damage)
    {
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        lastDamageTime = Time.time;
        health -= damage;
        health = Mathf.Max(health, 0);

        Shake();
        UpdateRopeScales();
        HealthChanged?.Invoke(this, health);
        if (health <= 0)
            Detach();
    }

    void Shake()
    {
        shakeTween?.Kill();
        shakeTween = transform.DOShakePosition(
            duration: shakeDuration,
            strength: shakeStrength,
            vibrato: shakeVibrato,
            randomness: shakeRandomness
        ).SetEase(Ease.OutQuad);
    }

    void UpdateRopeScales()
    {
        float healthPercent = (float)health / maxHealth; // 1 → 0
        float targetScaleY = Mathf.Lerp(minScaleY, maxScaleY, healthPercent);

        for (int i = 0; i < ropePieces.Length; i++)
        {
            Transform rope = ropePieces[i];
            if (rope == null) continue;

            Vector3 original = originalScales[i];
            Vector3 targetScale = new Vector3(original.x, targetScaleY * original.y, original.z);

            rope.DOScale(targetScale, scaleTweenDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    void Detach()
    {
        shakeTween?.Kill();
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
