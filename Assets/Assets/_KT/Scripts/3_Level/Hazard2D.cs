using UnityEngine;

/// <summary>
/// Опасность: шипы, лава, пила, враг, пропасть. Отнимает жизни у игрока (нужен PlayerHealth2D).
/// Работает и с обычным коллайдером (игрок стукается), и с триггером (игрок проходит сквозь).
/// Можно вешать прямо на Tilemap с шипами.
///
/// Пропасть: большой невидимый Box Collider 2D (Is Trigger) под уровнем + Hazard2D с галочкой Instant Kill.
/// Вода с плотами (вид сверху): Tilemap воды с триггером + Hazard2D с Instant Kill — на плоту игрок в безопасности.
/// Враг, которого можно убить прыжком на голову: включите Can Be Stomped.
/// </summary>
public class Hazard2D : KTBehaviour
{
    [Tooltip("Сколько жизней отнимает.")]
    [Min(0)]
    [SerializeField] private int damage = 1;

    [Tooltip("Убивает сразу, без учёта жизней. Ставьте для пропастей и лавы.")]
    [SerializeField] private bool instantKill = false;

    [Tooltip("Вид сверху: не трогать игрока, который стоит на плоту (MovingPlatform2D-триггере). Нужно для воды и лавы.")]
    [SerializeField] private bool safeOnPlatforms = true;

    [Header("Прыжок на голову (для врагов в платформере)")]
    [Tooltip("Если игрок прыгнул сверху — опасность уничтожается, а игрок отскакивает.")]
    [SerializeField] private bool canBeStomped = false;

    [Tooltip("С какой скоростью игрок отскакивает вверх.")]
    [SerializeField] private float stompBounce = 10f;

    [Tooltip("Что удалить при прыжке на голову. Если пусто — этот объект.")]
    [SerializeField] private GameObject objectToDestroy;

    [SerializeField] private GameObject stompEffectPrefab;
    [SerializeField] private AudioClip stompSound;

    private Collider2D ownCollider;
    private bool stomped;
    private Animator animator;
    private bool hasAttackTrigger;
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        ownCollider = GetComponent<Collider2D>();
        if (objectToDestroy == null) objectToDestroy = gameObject;

        // Если это враг с аниматором — при ударе по игроку играем анимацию атаки.
        animator = GetComponentInChildren<Animator>();
        hasAttackTrigger = KTUtils.AnimatorHasParameter(animator, "Attack", AnimatorControllerParameterType.Trigger);
    }

    private void OnCollisionEnter2D(Collision2D collision) => Touch(collision.collider, HitPoint(collision));
    private void OnCollisionStay2D(Collision2D collision) => Touch(collision.collider, HitPoint(collision));
    private void OnTriggerEnter2D(Collider2D other) => Touch(other, ClosestPointTo(other));
    private void OnTriggerStay2D(Collider2D other) => Touch(other, ClosestPointTo(other));

    private void Touch(Collider2D other, Vector2 hitPoint)
    {
        if (stomped || !KTUtils.IsPlayer(other)) return;

        if (safeOnPlatforms)
        {
            TopDownMovement2D rider = other.GetComponentInParent<TopDownMovement2D>();
            if (rider != null && rider.IsOnPlatform) return;
        }

        if (canBeStomped && IsStomp(other))
        {
            Stomp(other);
            return;
        }

        PlayerHealth2D health = other.GetComponentInParent<PlayerHealth2D>();
        if (health == null) return;

        if (hasAttackTrigger && !health.IsDead && !health.IsInvulnerable) animator.SetTrigger(AttackHash);

        if (instantKill) health.Kill();
        else health.TakeDamage(damage, hitPoint);
    }

    private bool IsStomp(Collider2D player)
    {
        // Прыжок на голову = ноги игрока выше середины опасности.
        return ownCollider != null && player.bounds.min.y >= ownCollider.bounds.center.y;
    }

    private void Stomp(Collider2D player)
    {
        stomped = true;

        Rigidbody2D playerBody = player.attachedRigidbody;
        if (playerBody != null) KTUtils.LaunchUp(playerBody, stompBounce);

        KTUtils.SpawnEffect(stompEffectPrefab, transform.position);
        KTUtils.PlaySound(stompSound);
        CameraFollow2D.Shake(0.1f, 0.15f);

        Destroy(objectToDestroy);
    }

    private Vector2 HitPoint(Collision2D collision)
    {
        return collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
    }

    private Vector2 ClosestPointTo(Collider2D other)
    {
        return ownCollider != null ? ownCollider.ClosestPoint(other.transform.position) : (Vector2)transform.position;
    }
}
