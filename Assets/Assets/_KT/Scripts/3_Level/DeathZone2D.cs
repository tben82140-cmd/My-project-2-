using UnityEngine;

/// <summary>
/// Смертельная зона: игрок, попавший сюда, сразу погибает. Работает в обоих вариантах.
/// Вешается на объект с коллайдером Is Trigger (галочка поставится сама) или прямо на Tilemap.
///
///   • Платформер: пропасть — растянутый Box Collider 2D под уровнем.
///   • Вид сверху: вода, лава, яма — Tilemap с Tilemap Collider 2D + Composite Collider 2D
///     (Is Trigger, Geometry Type = Polygons) + Rigidbody 2D Static.
///
/// Игрок на плоту или рассыпающемся полу над зоной в безопасности.
/// Есть PlayerHealth2D — игрок появится на последнем чекпоинте. Нет — просто вернётся на старт уровня.
/// Монеты, упавшие сюда, возвращаются на своё место, чтобы все монеты всегда можно было собрать.
/// Враги (с патрулём, погоней, Hazard2D или Damageable2D) погибают сразу. Платформы зона не трогает.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DeathZone2D : KTBehaviour
{
    [Tooltip("Вид сверху: не трогать игрока, который стоит на плоту или полу над зоной.")]
    [SerializeField] private bool safeOnPlatforms = true;

    [Tooltip("Монеты, упавшие в зону, возвращаются туда, где появились.")]
    [SerializeField] private bool returnCoins = true;

    [Tooltip("Враги, упавшие в зону, погибают: с патрулём, погоней, Hazard2D или Damageable2D.")]
    [SerializeField] private bool destroyEnemies = true;

    [Tooltip("Остальные падающие предметы (ящики и т. п.) тоже исчезают. Платформы, игрок и монеты не трогаются.")]
    [SerializeField] private bool destroyOtherObjects = true;

    [Tooltip("Звук падения (необязательно).")]
    [SerializeField] private AudioClip fallSound;

    private static Vector3 playerStartPosition;
    private static int playerStartLayer;
    private static int playerStartSortingLayerId;
    private static bool hasPlayerStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasPlayerStart = false;
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
        CompositeCollider2D composite = GetComponent<CompositeCollider2D>();
        if (composite != null) composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    private void Start()
    {
        // Запоминаем, где игрок начал уровень, — на случай, если у него нет PlayerHealth2D.
        if (hasPlayerStart) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        playerStartPosition = player.transform.position;
        playerStartLayer = player.layer;
        playerStartSortingLayerId = KTUtils.GetSortingLayerId(player);
        hasPlayerStart = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => Handle(other);
    private void OnTriggerStay2D(Collider2D other) => Handle(other);

    private void Handle(Collider2D other)
    {
        if (KTUtils.IsPlayer(other))
        {
            HandlePlayer(other);
            return;
        }

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || body.bodyType == RigidbodyType2D.Static) return;

        if (returnCoins)
        {
            CoinPickup coin = body.GetComponentInChildren<CoinPickup>();
            if (coin != null)
            {
                coin.ReturnToStart();
                return;
            }
        }

        // Платформы (движущиеся и рассыпающиеся) не трогаем — они вернутся на место сами.
        if (body.GetComponentInParent<IKTPlatform>() != null) return;

        GameObject target = body.gameObject;

        Damageable2D damageable = target.GetComponentInParent<Damageable2D>();
        if (damageable != null)
        {
            if (!destroyEnemies) return;
            KTUtils.PlaySound(fallSound);
            damageable.KillInstantly();
            return;
        }

        bool isEnemy = target.GetComponent<EnemyPatrol2D>() != null
                       || target.GetComponent<EnemyChaser2D>() != null
                       || target.GetComponent<EnemyAnimator2D>() != null
                       || target.GetComponentInChildren<Hazard2D>() != null;

        if (isEnemy ? destroyEnemies : destroyOtherObjects)
        {
            KTUtils.PlaySound(fallSound);
            Destroy(target);
        }
    }

    private void HandlePlayer(Collider2D other)
    {
        if (safeOnPlatforms)
        {
            TopDownMovement2D rider = other.GetComponentInParent<TopDownMovement2D>();
            if (rider != null && rider.IsOnPlatform) return;
        }

        PlayerHealth2D health = other.GetComponentInParent<PlayerHealth2D>();
        if (health != null)
        {
            if (health.IsDead) return;
            KTUtils.PlaySound(fallSound);
            health.Kill();
            return;
        }

        // Системы жизней нет — просто возвращаем игрока на старт уровня.
        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !hasPlayerStart) return;

        KTUtils.PlaySound(fallSound);
        body.linearVelocity = Vector2.zero;
        body.position = playerStartPosition;
        body.transform.position = playerStartPosition;
        KTUtils.ApplyLevel(body.gameObject, playerStartLayer, playerStartSortingLayerId);
        CameraFollow2D.SnapToTarget();
    }

    private void OnDrawGizmos()
    {
        // Красная заливка в окне Scene, чтобы зону было видно. У Tilemap рисуем только рамку.
        Collider2D zone = GetComponent<Collider2D>();
        if (zone == null) return;

        Bounds bounds = zone.bounds;
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (zone is BoxCollider2D || zone is CircleCollider2D || zone is CapsuleCollider2D)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
