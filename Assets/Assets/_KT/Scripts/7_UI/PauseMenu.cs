using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Пауза на Escape (на геймпаде — Start). Вешается на Canvas.
/// Сделайте панель паузы (Panel с кнопками «Продолжить», «Заново», «Выйти»), выключите её и перетащите в Pause Panel.
/// Кнопка «Продолжить» → On Click → PauseMenu.Resume.
/// </summary>
public class PauseMenu : KTBehaviour
{
    [Tooltip("Панель меню паузы.")]
    [SerializeField] private GameObject pausePanel;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private WinScreen2D winScreen;

    private void Start()
    {
        winScreen = FindAnyObjectByType<WinScreen2D>();
    }

    private void Update()
    {
        if (winScreen != null && winScreen.HasWon) return; // после победы пауза не нужна

        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        if (escape || start) Toggle();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
