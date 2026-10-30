
using UnityEngine;

public class Axe : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float startSpeed = 5f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private int damage = 1;

    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform enemyTarget;
    [SerializeField] private float arcHeight = 2f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem ropeHitParticles;
    [SerializeField] private ParticleSystem defaultHitParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] throwSounds;
    [SerializeField] private AudioClip[] ropeHitSounds;
    [SerializeField] private AudioClip[] shieldHitSounds;

    [Header("Settings")]
    [SerializeField] private float hitCooldown = 0.3f;

    private bool isThrown;
    private Animator anim;
    private float currentSpeed;
    private float speedMultiplier = 1f;
    private float flightProgress;
    private float flightDistance;

    private TrailRenderer trail;
    private float lastHitTime = -999f;

    private Vector3 arcStart;
    private Vector3 flightEnd;

    private Collider[] axeColliders;
    private Transform target;

    public bool IsFlying => isThrown;

    private void Awake()
    {
        arcStart = transform.position;

        anim = GetComponent<Animator>();

        trail = GetComponent<TrailRenderer>();

        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>();

        if (trail != null)
            trail.enabled = false;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        axeColliders = GetComponentsInChildren<Collider>(true);
    }

    public void Throw()
    {
        Throw(1f, false);
    }

    public void Throw(float speedMultiplier)
    {
        Throw(speedMultiplier, false);
    }

    public void Throw(float speedMultiplier, bool attackEnemy)
    {
        if (isThrown)
            return;

        target = attackEnemy ? enemyTarget : playerTarget;

        if (target == null)
        {
            Debug.LogError(
                $"Для топора {name} не назначена цель: " +
                (attackEnemy ? "Enemy Target" : "Player Target")
            );

            return;
        }

        arcStart = transform.position;
        flightEnd = target.position;

        flightDistance = Vector3.Distance(arcStart, flightEnd);

        if (flightDistance <= 0.001f)
        {
            Debug.LogWarning($"Слишком маленькая дистанция полёта: {name}");
            return;
        }

        flightProgress = 0f;

        this.speedMultiplier = Mathf.Max(0.05f, speedMultiplier);
        currentSpeed = startSpeed * this.speedMultiplier;

        SetCollidersEnabled(true);

        if (anim != null)
        {
            anim.SetBool("Flying", true);
            anim.SetBool("InShield", false);
        }

        isThrown = true;

        if (trail != null)
        {
            trail.Clear();
            trail.enabled = true;
        }

        PlayRandomSound(throwSounds);
    }

    private void Update()
    {
        if (!isThrown)
            return;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            maxSpeed * speedMultiplier,
            acceleration * speedMultiplier * Time.deltaTime
        );

        flightProgress += currentSpeed * Time.deltaTime / flightDistance;
        flightProgress = Mathf.Clamp01(flightProgress);

        Vector3 targetPos = Vector3.Lerp(
            arcStart,
            flightEnd,
            flightProgress
        );

        float yOffset = 4f * arcHeight *
            flightProgress * (1f - flightProgress);

        targetPos.y += yOffset;

        Vector3 movementDirection = targetPos - transform.position;

        if (movementDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(movementDirection);

        transform.position = targetPos;

        if (flightProgress >= 1f)
        {
            isThrown = false;

            SetCollidersEnabled(false);

            if (trail != null)
                trail.enabled = false;

            if (anim != null)
                anim.SetBool("Flying", false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isThrown)
            return;

        if (Time.time - lastHitTime < hitCooldown)
            return;

        lastHitTime = Time.time;
        isThrown = false;

        SetCollidersEnabled(false);

        if (trail != null)
            trail.enabled = false;

        if (anim != null)
        {
            anim.SetBool("Flying", false);
            anim.SetBool("InShield", false);
        }

        TapePiece tape = collision.gameObject.GetComponent<TapePiece>();

        if (tape == null)
            tape = collision.gameObject.GetComponentInParent<TapePiece>();

        if (tape != null)
        {
            tape.TakeDamage(damage);

            if (ropeHitParticles != null && collision.contactCount > 0)
            {
                Instantiate(
                    ropeHitParticles,
                    collision.contacts[0].point,
                    Quaternion.identity
                );
            }

            PlayRandomSound(ropeHitSounds);
        }
        else
        {
            if (defaultHitParticles != null && collision.contactCount > 0)
            {
                Instantiate(
                    defaultHitParticles,
                    collision.contacts[0].point,
                    Quaternion.identity
                );
            }

            if (anim != null)
                anim.SetBool("InShield", true);

            PlayRandomSound(shieldHitSounds);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (axeColliders == null)
            return;

        foreach (Collider axeCollider in axeColliders)
        {
            if (axeCollider != null)
                axeCollider.enabled = enabled;
        }
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.PlayOneShot(clip);
    }

    public void ResetFlight()
    {
        isThrown = false;
        currentSpeed = 0f;
        speedMultiplier = 1f;
        flightProgress = 0f;

        SetCollidersEnabled(false);

        if (trail != null)
            trail.enabled = false;

        if (anim != null)
        {
            anim.SetBool("Flying", false);
            anim.SetBool("InShield", false);
        }

        arcStart = transform.position;
        target = null;
    }
}
