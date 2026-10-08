using UnityEngine;

/// <summary>
/// Движущаяся платформа. Вешается на платформу; стартует со своего места и едет по точкам маршрута.
///
/// Платформер: обычный коллайдер (не триггер) — игрок стоит на ней и едет вместе с ней.
/// Вид сверху, коллайдер Is Trigger — это плот: игрок, стоящий на нём, едет вместе с ним.
/// Вид сверху, обычный коллайдер — движущееся препятствие: бревно, тележка, которая толкает игрока.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : KTBehaviour, IKTPlatform
{
    [Tooltip("Точки маршрута. Создайте пустые объекты и перетащите сюда. " +
             "Их можно сделать детьми платформы — положение запомнится на старте.")]
    [SerializeField] private Transform[] waypoints;

    [Tooltip("Скорость, единиц в секунду.")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Пауза в каждой точке, секунд.")]
    [SerializeField] private float waitTime = 0.5f;

    [Tooltip("Вкл — по кругу: старт → 1 → 2 → старт. Выкл — туда-обратно: старт → 1 → 2 → 1 → старт.")]
    [SerializeField] private bool loop = false;

    /// <summary>Текущая скорость платформы. Её читает скрипт игрока, чтобы ехать вместе с ней.</summary>
    public Vector2 Velocity { get; private set; }

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

        int count = waypoints != null ? waypoints.Length : 0;
        points = new Vector2[count + 1];
        points[0] = rb.position;
        for (int i = 0; i < count; i++)
        {
            points[i + 1] = waypoints[i] != null ? (Vector2)waypoints[i].position : rb.position;
        }

        if (count == 0)
        {
            Debug.LogWarning($"{name}: у MovingPlatform2D нет точек маршрута — платформа будет стоять.", this);
        }
    }

    private void FixedUpdate()
    {
        if (points.Length < 2 || Time.time < waitUntil)
        {
            Velocity = Vector2.zero;
            return;
        }

        Vector2 target = points[index];
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        Velocity = (next - rb.position) / Time.fixedDeltaTime;
        rb.MovePosition(next);

        if ((next - target).sqrMagnitude < 0.0001f)
        {
            waitUntil = Time.time + waitTime;
            NextPoint();
        }
    }

    private void NextPoint()
    {
        if (loop)
        {
            index = (index + 1) % points.Length;
            return;
        }

        if (index + direction >= points.Length || index + direction < 0)
        {
            direction = -direction;
        }
        index += direction;
    }

    private void OnDrawGizmos()
    {
        // Рисуем маршрут в окне Scene, чтобы было видно, куда поедет платформа.
        if (waypoints == null || waypoints.Length == 0) return;
        Gizmos.color = Color.cyan;
        Vector3 previous = Application.isPlaying && points != null ? (Vector3)points[0] : transform.position;
        foreach (Transform point in waypoints)
        {
            if (point == null) continue;
            Gizmos.DrawLine(previous, point.position);
            Gizmos.DrawWireSphere(point.position, 0.15f);
            previous = point.position;
        }
    }
}
