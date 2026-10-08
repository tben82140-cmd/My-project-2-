using UnityEngine;

/// <summary>
/// Плавное следование камеры за игроком + тряска + границы уровня. Вешается на Main Camera.
/// Игрок находится сам по тегу Player, если поле Target пустое.
/// Чтобы картинка не дрожала, у Rigidbody 2D игрока поставьте Interpolate = Interpolate.
/// </summary>
public class CameraFollow2D : KTBehaviour
{
    [Tooltip("За кем следить. Если пусто — найдётся объект с тегом Player.")]
    [SerializeField] private Transform target;

    [Tooltip("Сдвиг камеры относительно игрока. Например, (0, 1) — камера чуть выше.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);

    [Tooltip("Время «догоняния» в секундах. 0 — жёстко, 0.1–0.3 — плавно.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("Границы уровня (необязательно). Создайте пустой объект с Box Collider 2D (Is Trigger), " +
             "растяните на весь уровень и перетащите сюда — камера не будет показывать пустоту за краем.")]
    [SerializeField] private BoxCollider2D levelBounds;

    private static CameraFollow2D current;

    private Camera cam;
    private Vector3 basePosition;
    private Vector3 velocity;
    private float shakeTimeLeft;
    private float shakeDuration;
    private float shakeStrength;

    /// <summary>Тряхнуть камеру. Можно вызывать из любого скрипта: CameraFollow2D.Shake();</summary>
    public static void Shake(float strength = 0.15f, float duration = 0.2f)
    {
        if (current == null) return;
        current.shakeStrength = strength;
        current.shakeDuration = duration;
        current.shakeTimeLeft = duration;
    }

    /// <summary>Мгновенно перенести камеру к игроку (после телепорта или смерти).</summary>
    public static void SnapToTarget()
    {
        if (current != null) current.Snap();
    }

    private void Awake()
    {
        current = this;
        cam = GetComponent<Camera>();
        basePosition = transform.position;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void Start()
    {
        Snap();
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            basePosition = Vector3.SmoothDamp(basePosition, DesiredPosition(), ref velocity, smoothTime);
            basePosition = ClampToBounds(basePosition);
        }

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            shakeOffset = (Vector3)(Random.insideUnitCircle * (shakeStrength * fade));
        }

        transform.position = basePosition + shakeOffset;
    }

    private void Snap()
    {
        if (target == null) return;
        basePosition = ClampToBounds(DesiredPosition());
        velocity = Vector3.zero;
        transform.position = basePosition;
    }

    private Vector3 DesiredPosition()
    {
        // Z камеры не трогаем, иначе она «въедет» в спрайты.
        return new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
    }

    private Vector3 ClampToBounds(Vector3 position)
    {
        if (levelBounds == null || cam == null || !cam.orthographic) return position;

        Bounds bounds = levelBounds.bounds;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        position.x = ClampAxis(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = ClampAxis(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        return position;
    }

    private static float ClampAxis(float value, float min, float max)
    {
        // Если уровень меньше экрана — просто держим камеру по центру.
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
