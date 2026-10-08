using UnityEngine;

/// <summary>
/// Параллакс: дальний фон едет медленнее, чем уровень, и появляется ощущение глубины.
/// Вешается ОДИН раз на каждый слой фона (небо, горы, деревья вдали).
///
/// Бесконечный фон: включите Infinite Horizontal — и всё. При запуске скрипт сам поставит копии картинки
/// слева и справа (сколько нужно по ширине экрана), а когда экран уедет далеко — незаметно переставит слой
/// на одну картинку. Draw Mode = Tiled и ручные копии больше не нужны.
///
/// Можно и по-старому: пустой объект-родитель с копиями внутри — тогда скрипт вешается на родителя.
/// </summary>
[DefaultExecutionOrder(100)] // после камеры, чтобы фон не дёргался
public class ParallaxLayer2D : KTBehaviour
{
    [Tooltip("0 — слой стоит на месте вместе с уровнем, 1 — едет вместе с камерой (бесконечно далеко). " +
             "Небо ≈ 0.9, горы ≈ 0.7, ближние кусты ≈ 0.3. Отрицательное — слой ПЕРЕД уровнем.")]
    [SerializeField] private Vector2 parallaxFactor = new Vector2(0.5f, 0.5f);

    [Tooltip("Бесконечный фон по горизонтали: копии слева и справа ставятся сами при запуске.")]
    [SerializeField] private bool infiniteHorizontal = false;

    [Tooltip("Сколько копий поставить с каждой стороны. 0 — посчитать самому по ширине экрана (обычно так и надо).")]
    [Min(0)]
    [SerializeField] private int copiesEachSide = 0;

    [Tooltip("Ширина ОДНОЙ картинки в единицах — на столько слой перескакивает. 0 — определить самому.")]
    [SerializeField] private float repeatWidthOverride = 0f;

    private Transform cam;
    private Vector3 startPosition;
    private Vector3 cameraStart;
    private float repeatWidth;

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning($"{name}: нет камеры с тегом MainCamera — параллакс выключен.", this);
            enabled = false;
            return;
        }

        cam = Camera.main.transform;
        startPosition = transform.position;
        cameraStart = cam.position;

        SpriteRenderer own = GetComponent<SpriteRenderer>();
        repeatWidth = repeatWidthOverride > 0f ? repeatWidthOverride : MeasureRepeatWidth(own);

        if (GetComponentsInParent<ParallaxLayer2D>().Length > 1)
        {
            Debug.LogWarning($"{name}: ParallaxLayer2D висит и на этом объекте, и на его родителе — уберите лишний.", this);
        }

        if (!infiniteHorizontal) return;

        if (repeatWidth <= 0f)
        {
            Debug.LogWarning($"{name}: не получилось узнать ширину картинки для бесконечного фона. Укажите Repeat Width Override.", this);
            return;
        }

        // Своя картинка — ставим копии сами. Пустой родитель с копиями внутри — копии уже есть.
        if (own != null && own.sprite != null) CreateSideCopies(own);
    }

    private void LateUpdate()
    {
        Vector3 cameraMoved = cam.position - cameraStart;
        transform.position = new Vector3(
            startPosition.x + cameraMoved.x * parallaxFactor.x,
            startPosition.y + cameraMoved.y * parallaxFactor.y,
            startPosition.z);

        if (!infiniteHorizontal || repeatWidth <= 0f) return;

        // Экран уехал дальше одной картинки — переставляем слой на одну картинку (копии одинаковые, скачка не видно).
        float distanceToCamera = cam.position.x - transform.position.x;
        if (distanceToCamera > repeatWidth) startPosition.x += repeatWidth;
        else if (distanceToCamera < -repeatWidth) startPosition.x -= repeatWidth;
    }

    // ============================== Копии ==============================

    private void CreateSideCopies(SpriteRenderer source)
    {
        float copyWidth = CopyWidth(source) * Mathf.Abs(transform.lossyScale.x); // в мире
        int count = copiesEachSide > 0 ? copiesEachSide : CopiesNeeded(copyWidth);
        float localStep = CopyWidth(source); // в локальных координатах (масштаб наследуется от родителя)

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 1; i <= count; i++)
            {
                GameObject copy = new GameObject($"{name} (копия {(side < 0 ? "слева" : "справа")} {i})");
                copy.transform.SetParent(transform, false);
                copy.transform.localPosition = new Vector3(side * i * localStep, 0f, 0f);
                CopyRenderer(source, copy.AddComponent<SpriteRenderer>());
            }
        }
    }

    /// <summary>Сколько копий с каждой стороны нужно, чтобы край не попал в кадр даже перед перестановкой.</summary>
    private int CopiesNeeded(float copyWidth)
    {
        Camera camera = Camera.main;
        float halfScreen = camera.orthographic ? camera.orthographicSize * camera.aspect : copyWidth;
        // Слой переставляется, когда экран отъехал на repeatWidth; плюс половина экрана; минус половина самой картинки.
        float needed = (repeatWidth + halfScreen - copyWidth * 0.5f) / copyWidth;
        return Mathf.Max(1, Mathf.CeilToInt(needed) + 1); // +1 — запас на случай, если окно игры растянут
    }

    private static float CopyWidth(SpriteRenderer source)
    {
        return source.drawMode == SpriteDrawMode.Simple ? source.sprite.bounds.size.x : source.size.x;
    }

    private float MeasureRepeatWidth(SpriteRenderer own)
    {
        // Шаг повтора — ширина одной картинки (у Tiled-картинки узор повторяется через ширину спрайта).
        SpriteRenderer source = own != null ? own : GetComponentInChildren<SpriteRenderer>();
        if (source == null || source.sprite == null) return 0f;
        return source.sprite.bounds.size.x * Mathf.Abs(source.transform.lossyScale.x);
    }

    private static void CopyRenderer(SpriteRenderer from, SpriteRenderer to)
    {
        to.sprite = from.sprite;
        to.color = from.color;
        to.flipX = from.flipX;
        to.flipY = from.flipY;
        to.sharedMaterial = from.sharedMaterial;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder;
        to.maskInteraction = from.maskInteraction;
        to.spriteSortPoint = from.spriteSortPoint;
        to.drawMode = from.drawMode;
        if (from.drawMode != SpriteDrawMode.Simple)
        {
            to.tileMode = from.tileMode;
            to.size = from.size;
        }
    }

    // ============================== Подсказка в окне Scene ==============================

    private void OnDrawGizmosSelected()
    {
        // Показываем, где при запуске появятся копии.
        if (!infiniteHorizontal || Application.isPlaying) return;

        SpriteRenderer own = GetComponent<SpriteRenderer>();
        if (own == null || own.sprite == null) return;

        Bounds bounds = own.bounds;
        float step = bounds.size.x;
        int count = copiesEachSide > 0 ? copiesEachSide : 2;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        for (int i = 1; i <= count; i++)
        {
            Gizmos.DrawWireCube(bounds.center + Vector3.right * step * i, bounds.size);
            Gizmos.DrawWireCube(bounds.center - Vector3.right * step * i, bounds.size);
        }
    }
}
