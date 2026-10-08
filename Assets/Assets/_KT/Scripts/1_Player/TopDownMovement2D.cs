using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управление для вида сверху (вариант Б). Вешается на игрока.
/// Нужны: Rigidbody 2D (Dynamic, Gravity Scale = 0, Freeze Rotation Z, Linear Damping около 8),
/// коллайдер в районе ног, тег Player.
/// Управление: WASD или стрелки, рывок — Shift (если включён). Геймпад тоже работает.
///
/// Скрипт двигает игрока только СИЛАМИ (AddForce), а тормозит его Linear Damping у Rigidbody 2D.
/// Поэтому течение ручья (Area Effector 2D) спокойно сносит игрока поверх управления.
///
/// Плоты и рассыпающийся пол: если ноги игрока стоят на платформе с коллайдером Is Trigger
/// (MovingPlatform2D, FallingPlatform2D), игрок едет вместе с ней и смертельная зона под ней его не трогает.
/// Анимацией этот скрипт не занимается — для неё есть PlayerAnimator2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement2D : KTBehaviour, IKTMovement
{
    [Header("Движение")]
    [Tooltip("Скорость ходьбы, единиц в секунду.")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Рывок (необязательно)")]
    [Tooltip("Включить рывок на Shift (на геймпаде — правая кнопка: B или круг).")]
    [SerializeField] private bool dashEnabled = false;

    [Tooltip("Сила рывка.")]
    [SerializeField] private float dashPower = 14f;

    [Tooltip("Пауза между рывками, секунд.")]
    [SerializeField] private float dashCooldown = 0.6f;

    [Tooltip("Звук рывка (необязательно).")]
    [SerializeField] private AudioClip dashSound;

    public bool ControlEnabled { get; set; } = true;
    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsGrounded => true;
    public Vector2 MoveInput => ControlEnabled ? moveInput : Vector2.zero;

    /// <summary>Куда игрок смотрел в последний раз (для рывка и анимаций).</summary>
    public Vector2 FacingDirection => facing;

    /// <summary>Стоит ли игрок сейчас на плоту (движущейся платформе-триггере).</summary>
    public bool IsOnPlatform => currentPlatform != null;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction dashAction;
    private Vector2 moveInput;
    private Vector2 facing = Vector2.down;
    private bool dashRequested;
    private float lastDashTime = -10f;

    private Collider2D feetCollider;
    private IKTPlatform currentPlatform;
    private Vector2 lastPlatformVelocity;
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private ContactFilter2D triggerFilter;

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

        // Коллайдер ног — первый НЕ триггер на игроке. По его центру проверяем, стоим ли на плоту.
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (!col.isTrigger) { feetCollider = col; break; }
        }
        triggerFilter = new ContactFilter2D().NoFilter();

        dashAction = new InputAction("Dash", InputActionType.Button);
        dashAction.AddBinding("<Keyboard>/leftShift");
        dashAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void Start()
    {
        if (rb.gravityScale != 0f)
        {
            Debug.LogWarning($"{name}: у Rigidbody 2D Gravity Scale не 0 — при виде сверху игрок будет «падать» вниз экрана.", this);
        }
        if (rb.linearDamping <= 0f)
        {
            Debug.LogWarning($"{name}: у Rigidbody 2D Linear Damping = 0 — игрок не сможет остановиться. Поставьте около 8.", this);
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        dashAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        dashAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction.Dispose();
        dashAction.Dispose();
    }

    private void Update()
    {
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            facing = moveInput.normalized;
        }

        if (dashEnabled && dashAction.WasPressedThisFrame())
        {
            dashRequested = true;
        }
    }

    private void FixedUpdate()
    {
        RideMovingPlatform();

        if (!ControlEnabled)
        {
            dashRequested = false;
            return;
        }

        // При постоянной силе F и торможении Linear Damping игрок разгоняется до скорости F / (масса × damping).
        // Поэтому подбираем силу так, чтобы итоговая скорость была ровно moveSpeed.
        float force = moveSpeed * SpeedMultiplier * rb.linearDamping * rb.mass;
        rb.AddForce(moveInput * force);

        if (dashRequested && SpeedMultiplier < 0.01f) dashRequested = false; // во время удара рывка нет

        if (dashRequested)
        {
            dashRequested = false;
            if (Time.time - lastDashTime >= dashCooldown)
            {
                rb.AddForce(facing * (dashPower * rb.mass), ForceMode2D.Impulse);
                lastDashTime = Time.time;
                KTUtils.PlaySound(dashSound);
            }
        }
    }

    /// <summary>
    /// Катание на плоту. Скорость плота добавляется к скорости игрока сразу (импульсом) и
    /// поддерживается силой, чтобы Linear Damping её не «съедал». Поэтому игрок не съезжает
    /// с плота, когда тот трогается, тормозит или разворачивается.
    /// </summary>
    private void RideMovingPlatform()
    {
        currentPlatform = FindPlatformUnderFeet();
        Vector2 platformVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;

        Vector2 change = platformVelocity - lastPlatformVelocity;
        if (change.sqrMagnitude > 0.000001f)
        {
            rb.AddForce(change * rb.mass, ForceMode2D.Impulse);
        }

        rb.AddForce(platformVelocity * (rb.linearDamping * rb.mass));
        lastPlatformVelocity = platformVelocity;
    }

    private IKTPlatform FindPlatformUnderFeet()
    {
        Vector2 feet = feetCollider != null ? (Vector2)feetCollider.bounds.center : rb.position;

        overlaps.Clear();
        Physics2D.OverlapPoint(feet, triggerFilter, overlaps);

        foreach (Collider2D hit in overlaps)
        {
            if (!hit.isTrigger || hit.attachedRigidbody == rb) continue;
            IKTPlatform platform = hit.GetComponentInParent<IKTPlatform>();
            if (platform != null) return platform;
        }
        return null;
    }
}
