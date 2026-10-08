using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Счётчик монет. Один на сцену. Вешается на любой объект, например на Canvas.
/// Монеты сами регистрируются в нём (см. CoinPickup), считать их вручную не нужно.
/// </summary>
public class CoinCounter : KTBehaviour
{
    private static CoinCounter instance;

    public static CoinCounter Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CoinCounter>();
            }
            return instance;
        }
    }

    [Tooltip("Текст TextMeshPro на Canvas, куда выводится счёт.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("{0} — собрано, {1} — всего.")]
    [SerializeField] private string format = "Монеты: {0}/{1}";

    [Tooltip("Объект, который появится, когда собраны все монеты (необязательно). Например, текст «Уровень пройден!».")]
    [SerializeField] private GameObject winObject;

    [Header("События")]
    [Tooltip("Срабатывает при каждой собранной монете.")]
    [SerializeField] private UnityEvent onCoinCollected;

    [Tooltip("Срабатывает, когда собраны все монеты. Сюда можно повесить открытие двери (Door2D → Open), " +
             "остановку таймера (LevelTimer → StopTimer) или переход на следующий уровень (SceneLoader → LoadNextScene).")]
    [SerializeField] private UnityEvent onAllCoinsCollected;

    private int collectedCoins;
    private int totalCoins;
    private bool allCollected;

    public int Collected => collectedCoins;
    public int Total => totalCoins;

    private void Awake()
    {
        instance = this;
        if (winObject != null) winObject.SetActive(false);
        Refresh();
    }

    public void RegisterCoin()
    {
        totalCoins++;
        Refresh();
    }

    public void CollectCoin()
    {
        collectedCoins++;
        Refresh();
        onCoinCollected.Invoke();

        if (!allCollected && collectedCoins >= totalCoins)
        {
            allCollected = true;
            if (winObject != null) winObject.SetActive(true);
            onAllCoinsCollected.Invoke();
        }
    }

    private void Refresh()
    {
        if (label != null)
        {
            label.text = string.Format(format, collectedCoins, totalCoins);
        }
    }
}
