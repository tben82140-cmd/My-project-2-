using UnityEngine;

/// <summary>
/// Враг ходит туда-обратно по точкам маршрута. Вешается на врага.
/// Урон этот скрипт НЕ наносит — для этого добавьте на врага Hazard2D
/// (в платформере можно включить Can Be Stomped, чтобы врага можно было победить прыжком на голову).
/// В платформере ставьте точки маршрута на одной высоте — враг не падает, а идёт строго по маршруту.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol2D : KTBehaviour
{
    [Tooltip("Точки маршрута (пустые объекты). Враг стартует со своего места.")]
    [SerializeField] private Transform[] waypoints;

    [Tooltip("Скорость, единиц в секунду.")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Пауза в каждой точке, секунд.")]
    [SerializeField] private float waitTime = 0.3f;

    [Tooltip("Спрайт, который разворачивается по направлению движения. Если пусто — найдётся сам.")]
    [SerializeField] private SpriteRenderer spriteToFlip;

    [Tooltip("Куда смотрит враг на исходной картинке. Снимите галочку, если он нарисован смотрящим влево.")]
    [SerializeField] private bool spriteFacesRight = true;

    private Rigidbody2D rb;
    private Vector2[] points;
    private int index = 1;
    private int direction = 1;
    private float waitUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (spriteToFlip == null) spriteToFlip = GetComponentInChildren<SpriteRenderer>();
        if (GetComponent<EnemyAnimator2D>() != null) spriteToFlip = null; // разворотом занимается аниматор

        int count = waypoints != null ? waypoints.Length : 0;
        points = new Vector2[count + 1];
        points[0] = rb.position;
        for (int i = 0; i < count; i++)
        {
            points[i + 1] = waypoints[i] != null ? (Vector2)waypoints[i].position : rb.position;
        }
    }

    private void FixedUpdate()
    {
        if (points.Length < 2 || Time.time < waitUntil) return;

        Vector2 target = points[index];
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);

        float moveX = next.x - rb.position.x;
        if (spriteToFlip != null && Mathf.Abs(moveX) > 0.0001f)
        {
            bool lookingLeft = moveX < 0f;
            spriteToFlip.flipX = spriteFacesRight ? lookingLeft : !lookingLeft;
        }

        rb.MovePosition(next);

        if ((next - target).sqrMagnitude < 0.0001f)
        {
            waitUntil = Time.time + waitTime;
            if (index + direction >= points.Length || index + direction < 0) direction = -direction;
            index += direction;
        }
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.red;
        Vector3 previous = transform.position;
        foreach (Transform point in waypoints)
        {
            if (point == null) continue;
            Gizmos.DrawLine(previous, point.position);
            Gizmos.DrawWireSphere(point.position, 0.15f);
            previous = point.position;
        }
    }
}
