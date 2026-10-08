using TMPro;
using UnityEngine;

/// <summary>
/// Секундомер на экране — для спидрана. Вешается на Canvas.
/// Остановить, когда собраны все монеты: CoinCounter → On All Coins Collected → LevelTimer.StopTimer.
/// </summary>
public class LevelTimer : KTBehaviour
{
    [Tooltip("Текст TextMeshPro для времени.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Текст перед временем.")]
    [SerializeField] private string prefix = "Время: ";

    [Tooltip("Запустить сразу при старте сцены.")]
    [SerializeField] private bool startOnAwake = true;

    public float Elapsed { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        IsRunning = startOnAwake;
        Refresh();
    }

    private void Update()
    {
        if (!IsRunning) return;
        Elapsed += Time.deltaTime;
        Refresh();
    }

    public void StartTimer() => IsRunning = true;
    public void StopTimer() => IsRunning = false;

    public void ResetTimer()
    {
        Elapsed = 0f;
        Refresh();
    }

    private void Refresh()
    {
        if (label == null) return;
        int minutes = (int)(Elapsed / 60f);
        float seconds = Elapsed % 60f;
        label.text = $"{prefix}{minutes:00}:{seconds:00.00}";
    }
}
