using UnityEngine;

/// <summary>
/// Зона подбора монеты. Вешается на ДОЧЕРНИЙ объект монеты (PickupZone),
/// у которого есть Circle Collider 2D с галочкой Is Trigger.
///
/// Структура префаба монеты:
///   Coin            — Sprite Renderer, Rigidbody 2D, Circle Collider 2D (НЕ триггер, чтобы лежать на земле)
///   └─ PickupZone   — Circle Collider 2D (Is Trigger, радиус чуть больше), этот скрипт
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CoinPickup : KTBehaviour
{
    [Tooltip("Что удалить при сборе. Если пусто — удалится родитель (весь объект монеты).")]
    [SerializeField] private GameObject coinRoot;

    [Header("Сочность (необязательно)")]
    [Tooltip("Префаб эффекта при сборе, например Particle System. Удалится сам через 2 секунды.")]
    [SerializeField] private GameObject pickupEffectPrefab;

    [Tooltip("Звук при сборе.")]
    [SerializeField] private AudioClip pickupSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.8f;

    // Флаг «уже собрана». Нужен, потому что Destroy срабатывает только в конце кадра,
    // а игрок за этот кадр может задеть монету ещё раз (например, вторым коллайдером).
    private bool collected;
    private Vector3 startPosition;

    private void Reset()
    {
        // Срабатывает, когда скрипт только что добавили на объект: сразу делаем коллайдер триггером.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (coinRoot == null)
        {
            coinRoot = transform.parent != null ? transform.parent.gameObject : gameObject;
        }

        startPosition = coinRoot.transform.position;

        if (!GetComponent<Collider2D>().isTrigger)
        {
            Debug.LogWarning($"{name}: у коллайдера CoinPickup не стоит Is Trigger — монета не будет собираться.", this);
        }

        // Монета сама сообщает счётчику, что она существует. Так работают и монеты,
        // расставленные руками, и монеты, которые высыпаются из сундука.
        if (CoinCounter.Instance != null)
        {
            CoinCounter.Instance.RegisterCoin();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (!KTUtils.IsPlayer(other)) return;

        collected = true;
        GetComponent<Collider2D>().enabled = false;

        if (CoinCounter.Instance != null)
        {
            CoinCounter.Instance.CollectCoin();
        }

        KTUtils.SpawnEffect(pickupEffectPrefab, transform.position);
        KTUtils.PlaySound(pickupSound, soundVolume);

        Destroy(coinRoot);
    }

    /// <summary>Вернуть монету туда, где она появилась (её вызывает DeathZone2D).</summary>
    public void ReturnToStart()
    {
        if (collected) return;

        Rigidbody2D body = coinRoot.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = startPosition;
        }
        coinRoot.transform.position = startPosition;
    }
}
