using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Перезапуск уровня, переход на следующий, выход. Вешается на любой объект (например, на Canvas).
/// Методы удобно вызывать из кнопок UI (Button → On Click) и из событий других скриптов.
/// Важно: все сцены должны быть в списке сцен сборки (File → Build Profiles → Scene List).
/// </summary>
public class SceneLoader : KTBehaviour
{
    [Tooltip("Перезапускать уровень клавишей R.")]
    [SerializeField] private bool restartWithR = true;

    private void Update()
    {
        if (restartWithR && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartScene();
        }
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Следующая сцена по списку. После последней — снова первая.</summary>
    public void LoadNextScene()
    {
        Time.timeScale = 1f;
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
        SceneManager.LoadScene(next);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
