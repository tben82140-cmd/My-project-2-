using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управление для платформера (вариант А). Вешается на игрока.
/// Нужны: Rigidbody 2D (Dynamic), коллайдер, тег Player.
/// Управление: A/D или стрелки — бег, Space — прыжок. Геймпад тоже работает.
///
/// Скрипт двигает игрока только СИЛАМИ (AddForce) и никогда не задаёт скорость напрямую,
/// поэтому батут, толчки и движущиеся платформы работают поверх управления.
/// Анимацией этот скрипт не занимается — для неё есть PlayerAnimator2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlatformerMovement2D : KTBehaviour, IKTMovement
{
    [Header("Бег")]
    [Tooltip("Максимальная скорость бега, единиц в секунду.")]
    [SerializeField] private float maxSpeed = 6f;

    [Tooltip("Насколько резко набирается и сбрасывается скорость. Больше — резче.")]
    [SerializeField] private float acceleration = 12f;

    [Tooltip("Управление в воздухе: 0 — никакого, 1 — как на земле.")]
    [Range(0f, 1f)]
    [SerializeField] private float airControl = 0.6f;

    [Header("Прыжок")]
    [Tooltip("Высота прыжка в клетках. Скорость прыжка скрипт считает сам, поэтому высота не меняется, " +
             "даже если поменять Gravity Scale у Rigidbody 2D.")]
    [SerializeField] private float jumpHeight = 3f;

    [Tooltip("Дополнительные прыжки в воздухе: 0 — обычный прыжок, 1 — двойной, 2 — тройной.")]
    [Range(0, 3)]
    [SerializeField] private int extraJumps = 0;

    [Tooltip("Насколько обрезается прыжок, если рано отпустить кнопку. 0 — высота всегда одинаковая, 1 — сильно.")]
    [Range(0f, 1f)]
    [SerializeField] private float jumpCut = 0.5f;

    [Tooltip("Сколько секунд после схода с края ещё можно прыгнуть («время койота»).")]
    [SerializeField] private float coyoteTime = 0.1f;

    [Tooltip("Если нажать прыжок чуть раньше приземления, он сработает при касании земли.")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Tooltip("Звук прыжка (необязательно).")]
    [SerializeField] private AudioClip jumpSound;

    [Header("Падение")]
    [Tooltip("Во сколько раз падать быстрее, чем взлетать. 1 — как в жизни, 1.5–2 — резко, как в Марио. " +
             "С батутом ставьте Bounciness поменьше, иначе отскоки будут расти.")]
    [Range(1f, 3f)]
    [SerializeField] private float fallGravityMultiplier = 1f;

    [Tooltip("Максимальная скорость падения, единиц в секунду. Чтобы с большой высоты не пролетать сквозь пол.")]
    [SerializeField] private float maxFallSpeed = 20f;

    public bool ControlEnabled { get; set; } = true;
    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsGrounded => isGrounded;
    public Vector2 MoveInput => ControlEnabled ? new Vector2(moveInput, 0f) : Vector2.zero;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction jumpAction;

    private float moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isRisingFromJump;
    private int extraJumpsLeft;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private Vector2 groundVelocity;
    private IKTPlatform ridingPlatform;
    private float lastOnPlatformTime = -10f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private ContactFilter2D solidContacts;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        // Землёй считаем только настоящие коллайдеры, не триггеры.
        solidContacts = new ContactFilter2D();
        solidContacts.useTriggers = false;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
    }

    private void Update()
    {
        // Ввод читаем в Update (каждый кадр), а силы прикладываем в FixedUpdate (каждый шаг физики).
        moveInput = moveAction.ReadValue<Vector2>().x;
        jumpHeld = jumpAction.IsPressed();

        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            extraJumpsLeft = extraJumps;
        }

        if (ControlEnabled)
        {
            Run();
            TryJump();
        }

        StickToDescendingPlatform();
        CutJumpIfReleased();
        ApplyFallGravity();
    }

    private void Run()
    {
        // Толкаем игрока в сторону нужной скорости. Чем дальше от неё — тем сильнее толчок.
        // На движущейся платформе нужная скорость = скорость платформы + бег игрока.
        float control = isGrounded ? 1f : airControl;
        float targetSpeed = moveInput * maxSpeed * SpeedMultiplier + groundVelocity.x;
        float speedDifference = targetSpeed - rb.linearVelocity.x;
        rb.AddForce(Vector2.right * (speedDifference * acceleration * control * rb.mass));
    }

    /// <summary>
    /// Платформа, которая едет вниз, уходит из-под ног быстрее, чем игрок успевает упасть,
    /// и он начинает «подпрыгивать». Поэтому, пока игрок стоит на такой платформе (и не прыгает),
    /// подтягиваем его скорость вниз до скорости платформы.
    /// </summary>
    private void StickToDescendingPlatform()
    {
        if (ridingPlatform == null || isRisingFromJump) return;
        if (Time.time - lastOnPlatformTime > 0.1f) return;

        float platformSpeedY = ridingPlatform.Velocity.y;
        if (platformSpeedY >= 0f || rb.linearVelocity.y <= platformSpeedY) return;

        rb.AddForce(Vector2.up * ((platformSpeedY - rb.linearVelocity.y) * rb.mass), ForceMode2D.Impulse);
    }

    private void TryJump()
    {
        // Во время удара «с остановкой» прыгать нельзя — удар не отменяется.
        if (SpeedMultiplier < 0.01f) return;

        bool jumpWasPressed = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (!jumpWasPressed) return;

        bool canJumpFromGround = Time.time - lastGroundedTime <= coyoteTime;

        if (canJumpFromGround)
        {
            Jump();
            lastGroundedTime = -10f;
        }
        else if (extraJumpsLeft > 0)
        {
            extraJumpsLeft--;
            Jump();
        }
    }

    /// <summary>Скорость вверх, нужная, чтобы при текущей гравитации подпрыгнуть ровно на jumpHeight.</summary>
    private float JumpSpeed => Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale * Mathf.Max(0f, jumpHeight));

    private void ApplyFallGravity()
    {
        float speedY = rb.linearVelocity.y;
        if (speedY >= 0f) return;

        if (fallGravityMultiplier > 1f)
        {
            rb.AddForce(Physics2D.gravity * (rb.gravityScale * (fallGravityMultiplier - 1f) * rb.mass));
        }

        if (speedY < -maxFallSpeed)
        {
            rb.AddForce(Vector2.up * ((-maxFallSpeed - speedY) * rb.mass), ForceMode2D.Impulse);
        }
    }

    private void Jump()
    {
        KTUtils.LaunchUp(rb, JumpSpeed);
        isRisingFromJump = true;
        lastJumpPressedTime = -10f;
        KTUtils.PlaySound(jumpSound);
    }

    private void CutJumpIfReleased()
    {
        // Обрезаем только прыжок, сделанный игроком. Отскок от батута не трогаем.
        if (!isRisingFromJump) return;

        if (rb.linearVelocity.y <= 0f)
        {
            isRisingFromJump = false;
            return;
        }

        if (!jumpHeld && jumpCut > 0f)
        {
            rb.AddForce(Vector2.down * (rb.linearVelocity.y * jumpCut * rb.mass), ForceMode2D.Impulse);
            isRisingFromJump = false;
        }
    }

    private bool CheckGrounded()
    {
        // Земля — любой твёрдый коллайдер, который касается игрока СНИЗУ.
        // Поэтому не нужны ни отдельные физические слои, ни объект GroundCheck.
        groundVelocity = Vector2.zero;
        bool grounded = false;

        int count = rb.GetContacts(solidContacts, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y <= 0.5f) continue;

            grounded = true;

            Rigidbody2D other = contacts[i].rigidbody == rb ? contacts[i].otherRigidbody : contacts[i].rigidbody;
            if (other != null && other.TryGetComponent(out IKTPlatform platform))
            {
                groundVelocity = platform.Velocity;
                ridingPlatform = platform;
                lastOnPlatformTime = Time.time;
            }
        }
        return grounded;
    }
}
