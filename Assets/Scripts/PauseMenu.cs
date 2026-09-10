// PauseMenu.cs
// Menú de pausa: ESC para abrir/cerrar.
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public GameManager playback;

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

    // "Reiniciar": misma lógica que la tarjeta de fin -> re-corre con seed nueva.
    public void OnClickRestart()
    {
        ClosePause();

        StartScreen ss = FindAnyObjectByType<StartScreen>(FindObjectsInactive.Include);
        if (ss != null)
            ss.RestartWithNewSeed();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);   // fallback
    }

    // "Salir": vuelve a la pantalla principal (no cierra la app).
    public void OnClickQuit()
    {
        ClosePause();

        StartScreen ss = FindAnyObjectByType<StartScreen>(FindObjectsInactive.Include);
        if (ss != null)
        {
            ss.BackToMenu();
            return;
        }

        // fallback si no hay pantalla de inicio en la escena
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void ClosePause()
    {
        isPaused = false;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
