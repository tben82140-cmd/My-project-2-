using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Удар мечом, лапой, кулаком. Вешается на игрока (мастер аниматора добавит его сам, если включить «Есть удар»).
/// Бьёт всё, на чём есть Damageable2D, в той стороне, куда смотрит игрок.
/// Управление: J или левая кнопка мыши, на геймпаде — X / квадрат.
/// Если в аниматоре есть триггер Attack, скрипт сам запускает анимацию удара.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAttack2D : KTBehaviour
{
    [Header("Удар")]
    [Tooltip("Сколько урона наносит один удар.")]
    [Min(1)]
    [SerializeField] private int damage = 1;

    [Tooltip("Как далеко перед игроком центр удара (красный круг в окне Scene).")]
    [SerializeField] private float attackReach = 0.7f;

    [Tooltip("Радиус зоны удара.")]
    [SerializeField] private float attackRadius = 0.5f;

    [Tooltip("Сдвиг зоны удара, например на уровень рук: (0, 0.5).")]
    [SerializeField] private Vector2 attackOffset = Vector2.zero;

    [Tooltip("Пауза между ударами, секунд. Мастер аниматора подставит сюда длину клипа удара.")]
    [SerializeField] private float attackCooldown = 0.35f;

    [Tooltip("Через сколько секунд после нажатия проходит урон. Подгоните под кадр, где меч опускается.")]
    [SerializeField] private float hitDelay = 0.1f;

    public enum MoveWhileAttacking
    {
        [InspectorName("Свободно — бегать можно")] Free,
        [InspectorName("Медленно — сильно замедляется")] Slow,
        [InspectorName("Стоп — встаёт на месте")] Stop
    }

    [Tooltip("Что с движением во время удара. «Стоп» — как в большинстве игр: на земле герой встаёт и не может прыгнуть. " +
             "В воздухе (платформер) удар полёт не останавливает.")]
    [SerializeField] private MoveWhileAttacking movementWhileAttacking = MoveWhileAttacking.Stop;

    [Tooltip("Для режима «Медленно»: доля обычной скорости.")]
    [Range(0f, 1f)]
    [SerializeField] private float slowSpeedFactor = 0.3f;

    [Header("Сочность (необязательно)")]
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;

    [Tooltip("Эффект в месте попадания.")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Tooltip("Слегка трясти камеру при попадании.")]
    [SerializeField] private bool shakeOnHit = true;

    public bool IsAttacking => Time.time < lastAttackTime + attackCooldown;

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Rigidbody2D rb;
    private IKTMovement movement;
    private PlayerHealth2D health;
    private Animator animator;
    private bool hasAttackTrigger;
    private InputAction attackAction;
    private Vector2 facing = Vector2.right;
    private float lastAttackTime = -10f;

    private readonly List<Collider2D> hits = new List<Collider2D>();
    private readonly HashSet<Damageable2D> damagedThisSwing = new HashSet<Damageable2D>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<IKTMovement>();
        health = GetComponent<PlayerHealth2D>();
        animator = GetComponentInChildren<Animator>();
        hasAttackTrigger = KTUtils.AnimatorHasParameter(animator, "Attack", AnimatorControllerParameterType.Trigger);

        if (rb.gravityScale == 0f) facing = Vector2.down;

        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Keyboard>/j");
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/buttonWest");
    }

    private void OnEnable() => attackAction.Enable();
    private void OnDestroy() => attackAction.Dispose();

    private void Update()
    {
        UpdateFacing();

        if (attackAction.WasPressedThisFrame())
        {
            TryAttack();
        }
    }

    private void UpdateFacing()
    {
        if (movement == null || IsAttacking) return; // во время удара не разворачиваемся

        Vector2 input = movement.MoveInput;
        if (input.sqrMagnitude < 0.0001f) return;

        if (rb.gravityScale == 0f)
        {
            // Вид сверху: бьём в одну из четырёх сторон.
            facing = KTUtils.SnapToFourDirections(input);
        }
        else if (Mathf.Abs(input.x) > 0.01f)
        {
            // Платформер: бьём влево или вправо.
            facing = new Vector2(Mathf.Sign(input.x), 0f);
        }
    }

    private void TryAttack()
    {
        if (IsAttacking) return;
        if (health != null && health.IsDead) return;
        if (movement != null && !movement.ControlEnabled) return; // во время отбрасывания бить нельзя
        UpdateFacingNow();

        lastAttackTime = Time.time;
        if (hasAttackTrigger) animator.SetTrigger(AttackHash);
        KTUtils.PlaySound(swingSound);

        StartCoroutine(AttackRoutine(facing));
    }

    private IEnumerator AttackRoutine(Vector2 direction)
    {
        // В воздухе (платформер) удар не мешает полёту, на земле — замедляем или останавливаем.
        bool onGround = movement == null || movement.IsGrounded;
        if (movement != null && onGround)
        {
            if (movementWhileAttacking == MoveWhileAttacking.Stop) movement.SpeedMultiplier = 0f;
            else if (movementWhileAttacking == MoveWhileAttacking.Slow) movement.SpeedMultiplier = slowSpeedFactor;
        }

        if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);
        DealDamage(direction);

        float rest = attackCooldown - hitDelay;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        if (movement != null) movement.SpeedMultiplier = 1f;
    }

    private void OnDisable()
    {
        attackAction.Disable();
        // Если скрипт выключили посреди удара — не оставляем героя «замороженным».
        if (movement != null) movement.SpeedMultiplier = 1f;
    }

    private void UpdateFacingNow()
    {
        if (movement == null) return;
        Vector2 input = movement.MoveInput;
        if (input.sqrMagnitude < 0.0001f) return;
        facing = rb.gravityScale == 0f
            ? KTUtils.SnapToFourDirections(input)
            : (Mathf.Abs(input.x) > 0.01f ? new Vector2(Mathf.Sign(input.x), 0f) : facing);
    }

    private void DealDamage(Vector2 direction)
    {
        Vector2 center = AttackCenter(direction);

        hits.Clear();
        damagedThisSwing.Clear();
        Physics2D.OverlapCircle(center, attackRadius, new ContactFilter2D().NoFilter(), hits);

        foreach (Collider2D hit in hits)
        {
            if (hit.attachedRigidbody == rb) continue; // себя не бьём

            Damageable2D target = hit.GetComponentInParent<Damageable2D>();
            if (target == null || !damagedThisSwing.Add(target)) continue;

            target.TakeDamage(damage, transform.position);
        }

        if (damagedThisSwing.Count > 0)
        {
            KTUtils.PlaySound(hitSound);
            KTUtils.SpawnEffect(hitEffectPrefab, center);
            if (shakeOnHit) CameraFollow2D.Shake(0.08f, 0.1f);
        }
    }

    private Vector2 AttackCenter(Vector2 direction)
    {
        return (Vector2)transform.position + attackOffset + direction * attackReach;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 direction = Application.isPlaying ? facing : Vector2.right;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackCenter(direction), attackRadius);
    }
}
