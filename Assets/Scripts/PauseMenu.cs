// PauseMenu.cs
// Menú de pausa: ESC para abrir/cerrar.
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public PlaybackController playback;

    bool isPaused = false;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        if (playback != null)
        {
            if (isPaused) playback.Stop();
            else playback.Play();   // "Continuar" reanuda la reproducción automática
        }

        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void OnClickContinue()
    {
        TogglePause();
    }

    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickQuit()
    {
        Time.timeScale = 1f;
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
