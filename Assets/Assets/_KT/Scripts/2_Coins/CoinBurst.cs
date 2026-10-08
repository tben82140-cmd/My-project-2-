using System.Collections;
using UnityEngine;

/// <summary>
/// Сундук, из которого высыпаются монеты. Вешается на объект сундука. Работает в обоих вариантах:
///   вид сверху (у монеты Gravity Scale = 0) — монеты разлетаются во все стороны и тормозят Linear Damping;
///   платформер (Gravity Scale > 0) — монеты вылетают фонтаном вверх и падают на землю.
/// Монеты получают физический Layer и Sorting Layer сундука — так они работают и на холмах с уровнями высоты.
///
/// Хотите, чтобы сундук открывался по кнопке E? Снимите галочку Burst On Start,
/// добавьте на сундук Interactable2D и в его событие On Interact выберите CoinBurst → Burst.
/// </summary>
public class CoinBurst : KTBehaviour
{
    [Tooltip("Префаб монеты (вид сверху: Gravity Scale = 0, Linear Damping 2–4).")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Сколько монет выпадет.")]
    [Range(1, 20)]
    [SerializeField] private int coinCount = 8;

    [Tooltip("Точка, откуда вылетают монеты. Поставьте её чуть ПЕРЕД сундуком, а не внутри его коллайдера. Если пусто — центр сундука.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Минимальная сила вылета.")]
    [SerializeField] private float minForce = 3f;

    [Tooltip("Максимальная сила вылета.")]
    [SerializeField] private float maxForce = 6f;

    [Tooltip("Платформер: разброс «фонтана» монет вверх, в градусах. При виде сверху монеты летят во все стороны.")]
    [Range(0f, 90f)]
    [SerializeField] private float fountainSpread = 35f;

    [Tooltip("Пауза между монетами в секундах — так красивее, чем все разом.")]
    [SerializeField] private float delayBetweenCoins = 0.05f;

    [Tooltip("Высыпать монеты сразу при запуске сцены.")]
    [SerializeField] private bool burstOnStart = true;

    [Tooltip("Картинка открытого сундука (необязательно).")]
    [SerializeField] private Sprite openedSprite;

    [Tooltip("Звук открытия (необязательно).")]
    [SerializeField] private AudioClip openSound;

    private GameObject[] preparedCoins;
    private bool hasBurst;

    private void Start()
    {
        PrepareCoins();
        if (burstOnStart) Burst();
    }

    /// <summary>
    /// Создаём все монеты сразу и прячем их. Так счётчик с самого начала знает,
    /// сколько монет будет на уровне, даже если сундук ещё не открыт.
    /// </summary>
    private void PrepareCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning($"{name}: не назначен префаб монеты в CoinBurst.", this);
            return;
        }

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        preparedCoins = new GameObject[coinCount];

        for (int i = 0; i < coinCount; i++)
        {
            preparedCoins[i] = Instantiate(coinPrefab, origin, Quaternion.identity);
            CopyChestLayers(preparedCoins[i]);
            preparedCoins[i].SetActive(false);
        }
    }

    /// <summary>Высыпать монеты. Срабатывает только один раз.</summary>
    public void Burst()
    {
        if (hasBurst || preparedCoins == null) return;
        hasBurst = true;

        if (openedSprite != null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sprite = openedSprite;
        }
        KTUtils.PlaySound(openSound);

        StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        foreach (GameObject coin in preparedCoins)
        {
            if (coin == null) continue;
            coin.SetActive(true);

            Rigidbody2D coinBody = coin.GetComponent<Rigidbody2D>();
            if (coinBody != null)
            {
                Vector2 direction;
                if (KTUtils.IsTopDown(coinBody))
                {
                    direction = Random.insideUnitCircle.normalized;
                    if (direction == Vector2.zero) direction = Vector2.down;
                }
                else
                {
                    direction = Quaternion.Euler(0f, 0f, Random.Range(-fountainSpread, fountainSpread)) * Vector2.up;
                }
                float force = Random.Range(minForce, maxForce);
                coinBody.AddForce(direction * (force * coinBody.mass), ForceMode2D.Impulse);
            }

            if (delayBetweenCoins > 0f)
                yield return new WaitForSeconds(delayBetweenCoins);
        }
    }

    private void CopyChestLayers(GameObject coin)
    {
        if (gameObject.layer != 0) KTUtils.SetLayerRecursively(coin, gameObject.layer);

        SpriteRenderer chestRenderer = GetComponentInChildren<SpriteRenderer>();
        if (chestRenderer != null) KTUtils.SetSortingLayer(coin, chestRenderer.sortingLayerName);
    }
}
