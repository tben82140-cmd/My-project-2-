using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Жизни игрока: получение урона, мигание, отбрасывание, смерть и появление на чекпоинте.
/// Вешается на игрока. Урон наносят объекты со скриптом Hazard2D (шипы, враги, ямы).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth2D : KTBehaviour
{
    [Header("Жизни")]
    [Min(1)]
    [SerializeField] private int maxHealth = 3;

    [Tooltip("Сколько секунд после удара игрок неуязвим и мигает.")]
    [SerializeField] private float invulnerabilityTime = 1f;

    [Header("Отбрасывание при ударе")]
    [Tooltip("Сила отбрасывания. 0 — не отбрасывать.")]
    [SerializeField] private float knockbackForce = 7f;

    [Tooltip("На сколько секунд отключается управление после удара.")]
    [SerializeField] private float controlLockTime = 0.25f;

    [Header("Смерть")]
    [Tooltip("Вкл — после смерти сцена перезапускается целиком (сцена должна быть в списке сцен сборки). " +
             "Выкл — игрок появляется на последнем чекпоинте, собранные монеты остаются собранными.")]
    [SerializeField] private bool restartSceneOnDeath = false;

    [Tooltip("Пауза перед появлением заново, секунд.")]
    [SerializeField] private float respawnDelay = 0.6f;

    [Header("Интерфейс и сочность (необязательно)")]
    [Tooltip("Текст TextMeshPro, где показываются жизни.")]
    [SerializeField] private TMP_Text healthLabel;

    [Tooltip("{0} — сколько жизней, {1} — максимум.")]
    [SerializeField] private string healthFormat = "Жизни: {0}/{1}";

    [Tooltip("Трясти камеру при ударе (нужен CameraFollow2D на камере).")]
    [SerializeField] private bool shakeCamera = true;

    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;

    [Tooltip("Эффект при смерти, например взрыв частиц.")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("События")]
    [SerializeField] private UnityEvent onDamaged;
    [SerializeField] private UnityEvent onDied;
    [SerializeField] private UnityEvent onRespawned;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    private int health;
    private float invulnerableUntil;
    private bool isDead;
    private Vector3 respawnPoint;
    private int respawnLayer;
    private int respawnSortingLayerId;
    private Rigidbody2D rb;
    private IKTMovement movement;
    private SpriteRenderer[] renderers;
    private Coroutine blinkRoutine;
    private Animator animator;
    private bool hasHurtTrigger;
    private static readonly int HurtHash = Animator.StringToHash("Hurt");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<IKTMovement>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        animator = GetComponentInChildren<Animator>();
        hasHurtTrigger = KTUtils.AnimatorHasParameter(animator, "Hurt", AnimatorControllerParameterType.Trigger);
        respawnPoint = transform.position;
        respawnLayer = gameObject.layer;
        respawnSortingLayerId = KTUtils.GetSortingLayerId(gameObject);
        health = maxHealth;
        RefreshLabel();
    }

    /// <summary>Отнять жизни. Удобно вызывать из UnityEvent в инспекторе.</summary>
    public void Damage(int amount)
    {
        TakeDamage(amount, transform.position);
    }

    /// <summary>Отнять жизни и отбросить игрока от точки sourcePosition.</summary>
    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (isDead || IsInvulnerable || amount <= 0) return;

        health = Mathf.Max(0, health - amount);
        RefreshLabel();
        KTUtils.PlaySound(hurtSound);
        if (shakeCamera) CameraFollow2D.Shake();
        onDamaged.Invoke();

        if (health == 0)
        {
            Die();
            return;
        }

        invulnerableUntil = Time.time + invulnerabilityTime;
        if (hasHurtTrigger) animator.SetTrigger(HurtHash);
        StartBlink();
        Knockback(sourcePosition);
    }

    /// <summary>Добавить жизни (например, подобрали сердечко).</summary>
    public void Heal(int amount)
    {
        if (isDead) return;
        health = Mathf.Min(maxHealth, health + amount);
        RefreshLabel();
    }

    /// <summary>Убить сразу, без учёта жизней (ямы, пропасти).</summary>
    public void Kill()
    {
        if (isDead) return;
        health = 0;
        RefreshLabel();
        Die();
    }

    /// <summary>
    /// Запомнить точку, где игрок появится после смерти, и уровень высоты, на котором он сейчас стоит.
    /// Иначе погибший на холме игрок появился бы внизу «наверху» и прошёл бы сквозь стены.
    /// </summary>
    public void SetCheckpoint(Vector3 position)
    {
        respawnPoint = position;
        respawnLayer = gameObject.layer;
        respawnSortingLayerId = KTUtils.GetSortingLayerId(gameObject);
    }

    private void Knockback(Vector2 sourcePosition)
    {
        if (knockbackForce <= 0f) return;

        Vector2 direction = (Vector2)transform.position - sourcePosition;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.Normalize();

        // В платформере отбрасываем вбок и немного вверх — так понятнее и красивее.
        if (rb.gravityScale > 0f)
        {
            direction = new Vector2(direction.x >= 0f ? 1f : -1f, 1f).normalized;
        }

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * (knockbackForce * rb.mass), ForceMode2D.Impulse);

        if (movement != null && controlLockTime > 0f)
        {
            StartCoroutine(LockControl());
        }
    }

    private IEnumerator LockControl()
    {
        movement.ControlEnabled = false;
        yield return new WaitForSeconds(controlLockTime);
        if (!isDead) movement.ControlEnabled = true;
    }

    private void Die()
    {
        isDead = true;
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);

        KTUtils.PlaySound(deathSound);
        KTUtils.SpawnEffect(deathEffectPrefab, transform.position);
        if (shakeCamera) CameraFollow2D.Shake(0.3f, 0.3f);
        onDied.Invoke();

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (movement != null) movement.ControlEnabled = false;
        SetVisible(false);
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        yield return new WaitForSeconds(respawnDelay);

        if (restartSceneOnDeath)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        transform.position = respawnPoint;
        rb.position = respawnPoint;
        KTUtils.ApplyLevel(gameObject, respawnLayer, respawnSortingLayerId);
        rb.linearVelocity = Vector2.zero;
        rb.simulated = true;

        health = maxHealth;
        RefreshLabel();
        SetVisible(true);
        isDead = false;
        if (movement != null) movement.ControlEnabled = true;

        invulnerableUntil = Time.time + invulnerabilityTime;
        StartBlink();
        CameraFollow2D.SnapToTarget();
        onRespawned.Invoke();
    }

    private void StartBlink()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        bool visible = true;
        while (Time.time < invulnerableUntil)
        {
            visible = !visible;
            SetVisible(visible);
            yield return new WaitForSeconds(0.08f);
        }
        SetVisible(true);
        blinkRoutine = null;
    }

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = visible;
        }
    }

    private void RefreshLabel()
    {
        if (healthLabel != null)
        {
            healthLabel.text = string.Format(healthFormat, health, maxHealth);
        }
    }
}
