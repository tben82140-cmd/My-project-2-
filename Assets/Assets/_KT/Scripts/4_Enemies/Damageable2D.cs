using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Всё, что игрок может ударить: враги, ящики, горшки, кусты. Вешается на объект с коллайдером.
/// Урон наносит PlayerAttack2D. При ударе объект вспыхивает красным, при смерти — исчезает.
/// Если у объекта в аниматоре есть триггер Hurt, он запустится при ударе.
/// </summary>
public class Damageable2D : KTBehaviour
{
    [Tooltip("Сколько ударов выдерживает.")]
    [Min(1)]
    [SerializeField] private int maxHealth = 2;

    [Tooltip("Сколько секунд после удара объект неуязвим (чтобы один взмах не засчитался дважды).")]
    [SerializeField] private float invulnerabilityTime = 0.2f;

    [Tooltip("Отбрасывание от удара. Работает, если у объекта Dynamic Rigidbody 2D.")]
    [SerializeField] private float knockbackForce = 4f;

    [Tooltip("Цвет вспышки при ударе.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f);

    [Header("Смерть")]
    [Tooltip("Сколько секунд играть анимацию смерти перед исчезновением. 0 — исчезнуть сразу. " +
             "Мастер аниматора подставит сюда длину клипа смерти.")]
    [SerializeField] private float deathAnimationTime = 0f;

    [Tooltip("Что выпадет после уничтожения: сердечко, ключ (необязательно).")]
    [SerializeField] private GameObject dropPrefab;

    [Tooltip("Что удалить. Если пусто — этот объект.")]
    [SerializeField] private GameObject objectToDestroy;

    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;

    [Header("События")]
    [SerializeField] private UnityEvent onDamaged;
    [SerializeField] private UnityEvent onDied;

    public int Health => health;

    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private int health;
    private float invulnerableUntil;
    private bool dead;
    private Rigidbody2D rb;
    private Animator animator;
    private bool hasHurtTrigger;
    private bool hasDieTrigger;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    private void Awake()
    {
        health = maxHealth;
        if (objectToDestroy == null) objectToDestroy = gameObject;

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        hasHurtTrigger = KTUtils.AnimatorHasParameter(animator, "Hurt", AnimatorControllerParameterType.Trigger);
        hasDieTrigger = KTUtils.AnimatorHasParameter(animator, "Die", AnimatorControllerParameterType.Trigger);

        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
    }

    /// <summary>Нанести урон. sourcePosition — откуда пришёл удар (для отбрасывания).</summary>
    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (dead || Time.time < invulnerableUntil || amount <= 0) return;

        health -= amount;
        invulnerableUntil = Time.time + invulnerabilityTime;
        KTUtils.PlaySound(hitSound);
        onDamaged.Invoke();

        if (health <= 0)
        {
            Die();
            return;
        }

        if (hasHurtTrigger) animator.SetTrigger(HurtHash);
        Flash();
        Knockback(sourcePosition);
    }

    /// <summary>Нанести урон без отбрасывания. Удобно вызывать из UnityEvent.</summary>
    public void Damage(int amount)
    {
        TakeDamage(amount, transform.position);
    }

    /// <summary>Убить как от удара: с анимацией смерти и выпадением предмета.</summary>
    public void Kill()
    {
        if (dead) return;
        health = 0;
        Die();
    }

    /// <summary>
    /// Убить мгновенно — упал в пропасть или воду (её вызывает DeathZone2D).
    /// Без анимации смерти (иначе враг «застынет» в воздухе) и без выпадения предмета (он бы тоже упал).
    /// Событие On Died всё равно срабатывает.
    /// </summary>
    public void KillInstantly()
    {
        if (dead) return;
        dead = true;
        health = 0;
        KTUtils.PlaySound(deathSound);
        onDied.Invoke();
        Destroy(objectToDestroy);
    }

    private void Knockback(Vector2 sourcePosition)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || knockbackForce <= 0f) return;

        Vector2 direction = (Vector2)transform.position - sourcePosition;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        rb.AddForce(direction.normalized * (knockbackForce * rb.mass), ForceMode2D.Impulse);
    }

    private void Flash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = flashColor;
        }

        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = originalColors[i];
        }
        flashRoutine = null;
    }

    private void Die()
    {
        dead = true;
        KTUtils.PlaySound(deathSound);
        KTUtils.SpawnEffect(deathEffectPrefab, transform.position);
        if (dropPrefab != null) Instantiate(dropPrefab, transform.position, Quaternion.identity);
        onDied.Invoke();

        if (deathAnimationTime > 0f && hasDieTrigger)
        {
            // Доигрываем анимацию смерти: враг больше не двигается, не ранит и не мешает.
            animator.SetTrigger(DieHash);
            StopEverything();
            Destroy(objectToDestroy, deathAnimationTime);
        }
        else
        {
            Destroy(objectToDestroy);
        }
    }

    private void StopEverything()
    {
        foreach (Collider2D col in objectToDestroy.GetComponentsInChildren<Collider2D>()) col.enabled = false;
        foreach (Hazard2D hazard in objectToDestroy.GetComponentsInChildren<Hazard2D>()) hazard.enabled = false;
        foreach (EnemyPatrol2D patrol in objectToDestroy.GetComponentsInChildren<EnemyPatrol2D>()) patrol.enabled = false;
        foreach (EnemyChaser2D chaser in objectToDestroy.GetComponentsInChildren<EnemyChaser2D>()) chaser.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
    }
}
