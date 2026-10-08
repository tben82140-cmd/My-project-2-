using UnityEngine;

/// <summary>
/// Связывает движение игрока с Animator и разворачивает спрайт. Вешается на игрока.
/// Подходит к обоим вариантам КТ. Обычно его добавляет мастер «KT → Мастер аниматора».
///
/// Параметры аниматора (мастер создаёт их сам):
///   Speed (Float)         — скорость, когда игрок жмёт кнопки движения
///   VerticalSpeed (Float) — скорость вверх/вниз (прыжок и падение)
///   IsGrounded (Bool)     — стоит ли на земле
///   MoveX, MoveY (Float)  — куда смотрит: (0,-1) вниз, (0,1) вверх, (-1,0) влево, (1,0) вправо
///   Attack, Hurt (Trigger) — их запускают PlayerAttack2D и PlayerHealth2D
/// Если какого-то параметра в аниматоре нет — не страшно, скрипт его просто пропустит.
/// </summary>
[DefaultExecutionOrder(50)]
public class PlayerAnimator2D : KTBehaviour
{
    [Tooltip("Animator персонажа. Если пусто — найдётся сам на игроке или его детях.")]
    [SerializeField] private Animator animator;

    [Tooltip("Спрайт, который разворачивается влево-вправо. Если пусто — найдётся сам.")]
    [SerializeField] private SpriteRenderer spriteToFlip;

    [Tooltip("Разворачивать спрайт зеркально при движении влево. " +
             "Выключите, если у персонажа есть отдельные анимации «влево» и «вправо».")]
    [SerializeField] private bool flipSprite = true;

    [Tooltip("Куда смотрит персонаж на исходной картинке. Снимите галочку, если он нарисован смотрящим влево.")]
    [SerializeField] private bool spriteFacesRight = true;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    private Rigidbody2D rb;
    private IKTMovement movement;
    private PlayerAttack2D attack;
    private Vector2 lastDirection;

    private bool hasSpeed;
    private bool hasVerticalSpeed;
    private bool hasIsGrounded;
    private bool hasMoveX;
    private bool hasMoveY;

    private void Awake()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        movement = GetComponentInParent<IKTMovement>();
        attack = GetComponentInParent<PlayerAttack2D>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteToFlip == null) spriteToFlip = GetComponentInChildren<SpriteRenderer>();

        hasSpeed = KTUtils.AnimatorHasParameter(animator, "Speed", AnimatorControllerParameterType.Float);
        hasVerticalSpeed = KTUtils.AnimatorHasParameter(animator, "VerticalSpeed", AnimatorControllerParameterType.Float);
        hasIsGrounded = KTUtils.AnimatorHasParameter(animator, "IsGrounded", AnimatorControllerParameterType.Bool);
        hasMoveX = KTUtils.AnimatorHasParameter(animator, "MoveX", AnimatorControllerParameterType.Float);
        hasMoveY = KTUtils.AnimatorHasParameter(animator, "MoveY", AnimatorControllerParameterType.Float);

        // На старте при виде сверху персонаж смотрит вниз (на игрока), в платформере — вправо.
        lastDirection = rb != null && rb.gravityScale == 0f ? Vector2.down : Vector2.right;

        if (animator == null)
        {
            Debug.LogWarning($"{name}: PlayerAnimator2D не нашёл Animator. Запустите мастер: меню KT → Мастер аниматора.", this);
        }
    }

    private void Update()
    {
        if (rb == null) return;

        Vector2 velocity = rb.linearVelocity;
        Vector2 input = movement != null ? movement.MoveInput : velocity;
        bool wantsToMove = input.sqrMagnitude > 0.0001f;
        bool isTopDown = rb.gravityScale == 0f;
        bool isAttacking = attack != null && attack.IsAttacking;

        // Скорость считаем только когда игрок жмёт кнопки: на движущейся платформе
        // или в течении ручья персонаж стоит в Idle, а не бежит на месте.
        float speed = 0f;
        if (wantsToMove)
        {
            speed = isTopDown ? velocity.magnitude : Mathf.Abs(velocity.x);

            // Во время удара направление не меняем — иначе спрайт развернётся посреди взмаха.
            if (!isAttacking)
            {
                if (isTopDown) lastDirection = KTUtils.SnapToFourDirections(input);
                else if (Mathf.Abs(input.x) > 0.01f) lastDirection = new Vector2(Mathf.Sign(input.x), 0f);
            }
        }

        if (animator != null)
        {
            if (hasSpeed) animator.SetFloat(SpeedHash, speed);
            if (hasVerticalSpeed) animator.SetFloat(VerticalSpeedHash, velocity.y);
            if (hasIsGrounded) animator.SetBool(IsGroundedHash, movement == null || movement.IsGrounded);
            if (hasMoveX) animator.SetFloat(MoveXHash, lastDirection.x);
            if (hasMoveY) animator.SetFloat(MoveYHash, lastDirection.y);
        }

        UpdateFlip();
    }

    private void UpdateFlip()
    {
        if (spriteToFlip == null) return;

        if (!flipSprite)
        {
            spriteToFlip.flipX = false;
            return;
        }

        if (Mathf.Abs(lastDirection.x) > 0.01f)
        {
            bool lookingLeft = lastDirection.x < 0f;
            spriteToFlip.flipX = spriteFacesRight ? lookingLeft : !lookingLeft;
        }
        else
        {
            // Смотрит вверх или вниз — картинку не зеркалим.
            spriteToFlip.flipX = false;
        }
    }
}
