using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Финал уровня: экран «Уровень пройден!» со статистикой (монеты, время).
/// Вешается на Canvas. Проще всего собрать его кнопкой меню KT → Создать экран победы.
///
/// Когда срабатывает:
///   • «Собраны все монеты» — сразу, как только CoinCounter насчитал все монеты;
///   • «По команде» — когда кто-то вызовет Win: например, TriggerEvent2D на флажке финиша
///     (On Player Enter → WinScreen2D → Win). Так можно сделать «собери монеты → открылась дверь → дойди до финиша».
/// </summary>
public class WinScreen2D : KTBehaviour
{
    public enum WinCondition
    {
        [InspectorName("Собраны все монеты")] AllCoins,
        [InspectorName("По команде (финиш, дверь, событие)")] Manual
    }

    [Header("Когда победа")]
    [SerializeField] private WinCondition winCondition = WinCondition.AllCoins;

    [Header("Экран")]
    [Tooltip("Панель экрана победы. В начале уровня она выключается сама.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Текст со статистикой (необязательно).")]
    [SerializeField] private TMP_Text statsLabel;

    [Tooltip("{0} — собрано монет, {1} — всего, {2} — время (если на сцене есть LevelTimer).")]
    [SerializeField] private string statsFormat = "Монеты: {0}/{1}\nВремя: {2}";

    [Header("Что происходит при победе")]
    [Tooltip("Герой перестаёт слушаться управления.")]
    [SerializeField] private bool freezePlayer = true;

    [Tooltip("Через секунду после победы остановить весь мир (враги, платформы). Кнопки при этом работают.")]
    [SerializeField] private bool stopWorld = true;

    [SerializeField] private AudioClip winSound;

    [Tooltip("Эффект у игрока при победе, например конфетти из Particle System.")]
    [SerializeField] private GameObject celebrationEffect;

    [Tooltip("Через сколько секунд самому перейти на следующий уровень. 0 — ждать кнопку.")]
    [Min(0f)]
    [SerializeField] private float autoNextLevelDelay = 0f;

    [Header("События")]
    [SerializeField] private UnityEvent onWin;

    public bool HasWon { get; private set; }

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (HasWon || winCondition != WinCondition.AllCoins) return;

        CoinCounter counter = CoinCounter.Instance;
        if (counter != null && counter.Total > 0 && counter.Collected >= counter.Total) Win();
    }

    /// <summary>Победа! Можно вызывать из событий: флажок финиша, дверь, убитый босс.</summary>
    public void Win()
    {
        if (HasWon) return;
        HasWon = true;

        string time = "—";
        LevelTimer timer = FindAnyObjectByType<LevelTimer>();
        if (timer != null)
        {
            timer.StopTimer();
            time = FormatTime(timer.Elapsed);
        }

        CoinCounter counter = CoinCounter.Instance;
        int collected = counter != null ? counter.Collected : 0;
        int total = counter != null ? counter.Total : 0;
        if (statsLabel != null) statsLabel.text = string.Format(statsFormat, collected, total, time);

        if (panel != null) panel.SetActive(true);
        KTUtils.PlaySound(winSound);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            KTUtils.SpawnEffect(celebrationEffect, player.transform.position, 5f);

            if (freezePlayer)
            {
                IKTMovement movement = player.GetComponent<IKTMovement>();
                if (movement != null) movement.ControlEnabled = false;
            }
        }

        onWin.Invoke();
        StartCoroutine(AfterWin());
    }

    private IEnumerator AfterWin()
    {
        if (stopWorld)
        {
            yield return new WaitForSecondsRealtime(1f); // дать доиграть звуку и конфетти
            Time.timeScale = 0f;
        }

        if (autoNextLevelDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(autoNextLevelDelay);
            Time.timeScale = 1f;
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
            SceneManager.LoadScene(next);
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        return $"{minutes:00}:{seconds % 60f:00.00}";
    }
}
