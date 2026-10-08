using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Враг замечает игрока и бежит за ним. Вешается на врага. Работает в обоих вариантах:
/// при виде сверху бежит в любую сторону, в платформере — только влево-вправо (вариант берётся по игроку).
///
/// Можно вместе с EnemyPatrol2D: пока игрока не видно — враг патрулирует, заметил — гонится,
/// потерял — возвращается к патрулю.
/// Урон этот скрипт НЕ наносит — добавьте на врага Hazard2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser2D : KTBehaviour
{
    [Header("Погоня")]
    [Tooltip("Скорость погони, единиц в секунду.")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Насколько резко враг разгоняется и поворачивает.")]
    [SerializeField] private float acceleration = 8f;

    [Tooltip("С какого расстояния враг замечает игрока (жёлтый круг в окне Scene).")]
    [SerializeField] private float detectRadius = 4f;

    [Tooltip("На каком расстоянии враг теряет игрока (серый круг). Должен быть больше Detect Radius.")]
    [SerializeField] private float loseRadius = 6f;

    [Tooltip("Видит игрока только если между ними нет стен.")]
    [SerializeField] private bool needsLineOfSight = false;

    [Tooltip("Потеряв игрока, возвращаться на место. Если на враге есть EnemyPatrol2D — он просто вернётся к патрулю.")]
    [SerializeField] private bool returnHome = true;

    [Header("Внешний вид (необязательно)")]
    [Tooltip("Что показать, пока враг гонится, — например «!» над головой.")]
    [SerializeField] private GameObject alertIndicator;

    [Tooltip("Звук, когда враг заметил игрока.")]
    [SerializeField] private AudioClip alertSound;

    [Tooltip("Спрайт, который разворачивается. Если пусто — найдётся сам.")]
    [SerializeField] private SpriteRenderer spriteToFlip;

    [SerializeField] private bool spriteFacesRight = true;

    public bool IsChasing => chasing;

    private Rigidbody2D rb;
    private Rigidbody2D playerBody;
    private Transform player;
    private EnemyPatrol2D patrol;
    private Vector2 home;
    private bool chasing;
    private bool warnedNoPlayer;

    private readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        patrol = GetComponent<EnemyPatrol2D>();
        home = rb.position;
        if (spriteToFlip == null) spriteToFlip = GetComponentInChildren<SpriteRenderer>();
        if (GetComponent<EnemyAnimator2D>() != null) spriteToFlip = null; // разворотом занимается аниматор
        if (alertIndicator != null) alertIndicator.SetActive(false);

        if (rb.bodyType == RigidbodyType2D.Static)
        {
            Debug.LogWarning($"{name}: у Rigidbody 2D врага Body Type = Static — он не сможет двигаться. Поставьте Dynamic.", this);
        }
    }

    private void FixedUpdate()
    {
        if (player == null && !FindPlayer()) return;

        UpdateChasingState();

        if (patrol != null)
        {
            // Пока не гонимся — пусть ходит патруль.
            patrol.enabled = !chasing;
            if (!chasing) return;
        }

        Vector2 target = rb.position;
        if (chasing) target = player.position;
        else if (returnHome) target = home;

        Move(target);
    }

    private void UpdateChasingState()
    {
        float distance = Vector2.Distance(rb.position, player.position);
        bool wasChasing = chasing;

        if (!chasing && distance <= detectRadius && CanSeePlayer()) chasing = true;
        else if (chasing && (distance >= loseRadius || !CanSeePlayer())) chasing = false;

        if (chasing == wasChasing) return;

        SetPhysicsForChase(chasing);
        if (alertIndicator != null) alertIndicator.SetActive(chasing);
        if (chasing) KTUtils.PlaySound(alertSound);
    }

    private void Move(Vector2 target)
    {
        // Вариант определяем по игроку: у врага-Kinematic гравитация ничего не говорит.
        bool horizontalOnly = !KTUtils.IsTopDown(playerBody != null ? playerBody : rb);

        Vector2 toTarget = target - rb.position;
        if (horizontalOnly) toTarget.y = 0f;

        Vector2 desiredVelocity = toTarget.magnitude > 0.2f ? toTarget.normalized * moveSpeed : Vector2.zero;

        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            // Kinematic (например, после EnemyPatrol2D) не реагирует на силы — двигаем напрямую.
            rb.MovePosition(rb.position + desiredVelocity * Time.fixedDeltaTime);
        }
        else
        {
            Vector2 velocityDifference = desiredVelocity - rb.linearVelocity;
            if (horizontalOnly) velocityDifference.y = 0f;
            rb.AddForce(velocityDifference * (acceleration * rb.mass));
        }

        if (spriteToFlip != null && Mathf.Abs(desiredVelocity.x) > 0.01f)
        {
            bool lookingLeft = desiredVelocity.x < 0f;
            spriteToFlip.flipX = spriteFacesRight ? lookingLeft : !lookingLeft;
        }
    }

    /// <summary>
    /// С патрулём: патруль ходит Kinematic-телом (сквозь стены не нужно — маршрут задан точками),
    /// а в погоне враг должен упираться в стены, поэтому на время погони тело становится Dynamic.
    /// </summary>
    private void SetPhysicsForChase(bool chase)
    {
        if (patrol == null) return;

        if (chase && rb.bodyType != RigidbodyType2D.Dynamic)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.freezeRotation = true;
            if (KTUtils.IsTopDown(playerBody)) rb.gravityScale = 0f;
        }
        else if (!chase && rb.bodyType != RigidbodyType2D.Kinematic)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private bool CanSeePlayer()
    {
        if (!needsLineOfSight) return true;

        ContactFilter2D solidOnly = new ContactFilter2D();
        solidOnly.useTriggers = false;
        sightHits.Clear();
        Physics2D.Linecast(rb.position, player.position, solidOnly, sightHits);

        foreach (RaycastHit2D hit in sightHits)
        {
            Rigidbody2D hitBody = hit.collider.attachedRigidbody;
            if (hitBody == rb || hitBody == playerBody) continue; // себя и игрока не считаем
            return false; // между нами стена
        }
        return true;
    }

    private bool FindPlayer()
    {
        // Ищем каждый шаг, пока не найдём: игрок мог появиться позже врага.
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            if (!warnedNoPlayer)
            {
                Debug.LogWarning($"{name}: враг не нашёл игрока — у игрока нет тега Player.", this);
                warnedNoPlayer = true;
            }
            return false;
        }

        player = playerObject.transform;
        playerBody = playerObject.GetComponent<Rigidbody2D>();

        // Вид сверху, а у врага включена гравитация — он «упадёт» вниз экрана. Исправляем сами.
        if (KTUtils.IsTopDown(playerBody) && rb.bodyType == RigidbodyType2D.Dynamic && rb.gravityScale != 0f)
        {
            Debug.LogWarning($"{name}: вид сверху, а у врага Gravity Scale {rb.gravityScale}. Ставлю 0, чтобы он не уезжал вниз.", this);
            rb.gravityScale = 0f;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseRadius);

        if (Application.isPlaying && player != null)
        {
            Gizmos.color = chasing ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, player.position);
        }
    }
}
