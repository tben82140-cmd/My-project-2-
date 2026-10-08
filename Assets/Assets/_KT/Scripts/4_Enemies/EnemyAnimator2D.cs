using UnityEngine;

/// <summary>
/// Связывает движение врага с Animator и разворачивает спрайт. Вешается на врага.
/// Обычно его добавляет мастер «KT → Мастер аниматора» в режиме «Враг».
///
/// Скорость считается по тому, как враг реально сдвинулся, поэтому работает с любым движением:
/// EnemyPatrol2D, EnemyChaser2D, движущейся платформой, отбрасыванием от удара.
///
/// Параметры аниматора (мастер создаёт их сам):
///   Speed, VerticalSpeed (Float), IsGrounded, IsChasing (Bool), MoveX, MoveY (Float).
/// Триггеры Attack, Hurt, Die запускают Hazard2D и Damageable2D.
/// </summary>
[DefaultExecutionOrder(50)]
public class EnemyAnimator2D : KTBehaviour
{
    [Tooltip("Animator врага. Если пусто — найдётся сам.")]
    [SerializeField] private Animator animator;

    [Tooltip("Спрайт, который разворачивается. Если пусто — найдётся сам.")]
    [SerializeField] private SpriteRenderer spriteToFlip;

    [Tooltip("Зеркалить спрайт при движении влево. Выключите, если есть отдельные анимации «влево» и «вправо».")]
    [SerializeField] private bool flipSprite = true;

    [Tooltip("Куда смотрит враг на исходной картинке. Снимите галочку, если он нарисован смотрящим влево.")]
    [SerializeField] private bool spriteFacesRight = true;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");

    private Rigidbody2D rb;
    private EnemyChaser2D chaser;
    private bool isTopDown;
    private Vector2 lastPosition;
    private Vector2 velocity;
    private Vector2 lastDirection;
    private bool isGrounded = true;

    private bool hasSpeed, hasVerticalSpeed, hasIsGrounded, hasIsChasing, hasMoveX, hasMoveY;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    private ContactFilter2D solidContacts;

    private void Awake()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        chaser = GetComponentInParent<EnemyChaser2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteToFlip == null) spriteToFlip = GetComponentInChildren<SpriteRenderer>();

        hasSpeed = KTUtils.AnimatorHasParameter(animator, "Speed", AnimatorControllerParameterType.Float);
        hasVerticalSpeed = KTUtils.AnimatorHasParameter(animator, "VerticalSpeed", AnimatorControllerParameterType.Float);
        hasIsGrounded = KTUtils.AnimatorHasParameter(animator, "IsGrounded", AnimatorControllerParameterType.Bool);
        hasIsChasing = KTUtils.AnimatorHasParameter(animator, "IsChasing", AnimatorControllerParameterType.Bool);
        hasMoveX = KTUtils.AnimatorHasParameter(animator, "MoveX", AnimatorControllerParameterType.Float);
        hasMoveY = KTUtils.AnimatorHasParameter(animator, "MoveY", AnimatorControllerParameterType.Float);

        solidContacts = new ContactFilter2D();
        solidContacts.useTriggers = false;
    }

    private void Start()
    {
        // Вариант игры берём по игроку: у врага-Kinematic гравитация ничего не говорит.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Rigidbody2D playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        isTopDown = KTUtils.IsTopDown(playerBody != null ? playerBody : rb);

        lastDirection = isTopDown ? Vector2.down : Vector2.right;
        if (rb != null) lastPosition = rb.position;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        velocity = (rb.position - lastPosition) / Time.fixedDeltaTime;
        lastPosition = rb.position;
        isGrounded = CheckGrounded();
    }

    private void Update()
    {
        float speed = isTopDown ? velocity.magnitude : Mathf.Abs(velocity.x);
        if (speed > 0.05f)
        {
            lastDirection = isTopDown
                ? KTUtils.SnapToFourDirections(velocity)
                : new Vector2(Mathf.Sign(velocity.x), 0f);
        }

        if (animator != null)
        {
            if (hasSpeed) animator.SetFloat(SpeedHash, speed);
            if (hasVerticalSpeed) animator.SetFloat(VerticalSpeedHash, velocity.y);
            if (hasIsGrounded) animator.SetBool(IsGroundedHash, isGrounded);
            if (hasIsChasing) animator.SetBool(IsChasingHash, chaser != null && chaser.IsChasing);
            if (hasMoveX) animator.SetFloat(MoveXHash, lastDirection.x);
            if (hasMoveY) animator.SetFloat(MoveYHash, lastDirection.y);
        }

        UpdateFlip();
    }

    private bool CheckGrounded()
    {
        // Kinematic-враги и враги при виде сверху «всегда на земле».
        if (isTopDown || rb.bodyType != RigidbodyType2D.Dynamic || rb.gravityScale == 0f) return true;

        int count = rb.GetContacts(solidContacts, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y > 0.5f) return true;
        }
        return false;
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
            spriteToFlip.flipX = false;
        }
    }
}
