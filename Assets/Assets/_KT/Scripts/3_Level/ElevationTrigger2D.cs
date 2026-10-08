using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Лестница между уровнями высоты (вид сверху). Вешается на объект с Box Collider 2D, который
/// накрывает ВСЮ лестницу. Обычно его ставит само окно KT → Уровни высоты.
///
/// Каждый шаг физики скрипт смотрит, где ноги игрока:
///   • ноги на лестнице — игрок на ВЕРХНЕМ уровне и рисуется поверх холма (не прячется за ним при спуске);
///   • сошёл с лестницы на верх холма — остаётся наверху;
///   • сошёл куда угодно ещё (вниз, вбок на землю) — переходит на НИЖНИЙ уровень.
/// Форма лестницы может быть любой: прямая, боковой склон, уголком — зона повторяет клетки лестницы.
/// Переход меняет физический Layer игрока (с какими стенами он сталкивается) и Sorting Layer его спрайтов.
///
/// Нужно заранее (окно KT → Уровни высоты делает это кнопкой «Подготовить проект»):
///   Layers и Sorting Layers «Level1», «Level2», и уровни не сталкиваются друг с другом в Layer Collision Matrix.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ElevationTrigger2D : KTBehaviour
{
    [Header("Верхний уровень (холм)")]
    [Tooltip("Физический Layer игрока на лестнице и на холме.")]
    [SerializeField] private string layerAbove = "Level2";

    [Tooltip("Sorting Layer игрока на лестнице и на холме.")]
    [SerializeField] private string sortingLayerAbove = "Level2";

    [Header("Нижний уровень (земля)")]
    [SerializeField] private string layerBelow = "Level1";
    [SerializeField] private string sortingLayerBelow = "Level1";

    [Tooltip("Tilemap верха холма. Сойдя с лестницы на его клетки, игрок остаётся наверху. Окно KT ставит его само.")]
    [SerializeField] private Tilemap hillTop;

    [Tooltip("Запасной вариант, если Hill Top не задан: где у лестницы «верх». Обычно (0, 1).")]
    [SerializeField] private Vector2 upDirection = Vector2.up;

    [Tooltip("Писать в Console каждую смену уровня. Удобно, чтобы понять, что происходит на лестнице.")]
    [SerializeField] private bool logLevelChanges = true;

    private Collider2D area;
    private GameObject player;
    private Rigidbody2D playerBody;
    private Collider2D playerFeet;
    private bool playerWasOnStairs;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        // Если зона собрана из клеток (несколько Box Collider 2D + Composite) — проверяем по Composite.
        area = GetComponent<CompositeCollider2D>();
        if (area == null) area = GetComponent<Collider2D>();
        area.isTrigger = true;
    }

    private void Start()
    {
        FindPlayer();
        if (player != null) playerWasOnStairs = area.OverlapPoint(FeetPosition());
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        Vector2 feet = FeetPosition();
        bool onStairs = area.OverlapPoint(feet);

        if (onStairs)
        {
            SetLevel(layerAbove, sortingLayerAbove);
        }
        else if (playerWasOnStairs)
        {
            // Только что сошёл с лестницы — куда именно?
            if (LeftOntoHill(feet)) SetLevel(layerAbove, sortingLayerAbove);
            else SetLevel(layerBelow, sortingLayerBelow);
        }

        playerWasOnStairs = onStairs;
    }

    private bool LeftOntoHill(Vector2 feet)
    {
        if (hillTop != null) return hillTop.HasTile(hillTop.WorldToCell(feet));
        return LeftTowardsTop(feet);
    }

    private bool LeftTowardsTop(Vector2 feet)
    {
        Bounds bounds = area.bounds;
        Vector2 up = upDirection.sqrMagnitude > 0f ? upDirection.normalized : Vector2.up;
        float halfLength = Mathf.Abs(up.x) * bounds.extents.x + Mathf.Abs(up.y) * bounds.extents.y;
        float alongUp = Vector2.Dot(feet - (Vector2)bounds.center, up);
        return alongUp >= halfLength - 0.05f;
    }

    private Vector2 FeetPosition()
    {
        if (playerFeet != null && playerFeet.enabled) return playerFeet.bounds.center;
        return playerBody != null ? playerBody.position : (Vector2)player.transform.position;
    }

    private void FindPlayer()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        playerBody = player.GetComponent<Rigidbody2D>();
        playerFeet = null;
        foreach (Collider2D col in player.GetComponentsInChildren<Collider2D>())
        {
            if (!col.isTrigger) { playerFeet = col; break; }
        }
    }

    private void SetLevel(string layerName, string sortingLayerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"{name}: нет физического Layer «{layerName}». Нажмите KT → Уровни высоты → «Подготовить проект».", this);
        }
        else if (player.layer != layer)
        {
            if (logLevelChanges)
                Debug.Log($"[Лестница] {name}: игрок {LayerMask.LayerToName(player.layer)} → {layerName}", this);
            KTUtils.SetLayerRecursively(player, layer);
        }

        if (string.IsNullOrEmpty(sortingLayerName)) return;
        int sortingId = SortingLayer.NameToID(sortingLayerName);
        if (!SortingLayer.IsValid(sortingId)) return;

        foreach (SpriteRenderer spriteRenderer in player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer.sortingLayerID != sortingId) spriteRenderer.sortingLayerID = sortingId;
        }
    }

    private void OnDrawGizmos()
    {
        Collider2D zone = GetComponent<CompositeCollider2D>();
        if (zone == null) zone = GetComponent<Collider2D>();
        if (zone == null) return;

        // Жёлтая рамка — лестница, стрелка — где у неё «верх».
        Bounds bounds = zone.bounds;
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Vector3 start = bounds.center;
        Vector3 end = start + (Vector3)(upDirection.normalized * Mathf.Min(bounds.extents.x, bounds.extents.y));
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.06f);
    }
}
