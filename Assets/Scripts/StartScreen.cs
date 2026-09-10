// StartScreen.cs
// Pantalla de inicio con dos botones: estrategia mejorada y random.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartScreen : MonoBehaviour
{
    [Header("Referencias")]
    public JsonLoader jsonLoader;
    public GameManager playbackController;

    [Header("UI")]
    public GameObject startPanel;
    [Tooltip("Imagen de portada a pantalla completa (con el FLASHPOINT). Se oculta al empezar.")]
    public GameObject background;
    public Button playButton;
    public Button randomButton;
    public TMP_Text loadingText;

    [Header("HUD (se oculta hasta que empiece la partida)")]
    public GameObject hudCanvas;

    bool lastUseRandom = false;

    void Start()
    {
        if (background == null)
            background = GameObject.Find("BackGround");

        if (background != null)
            background.SetActive(true);

        if (startPanel != null)
            startPanel.SetActive(true);

        if (hudCanvas != null)
            hudCanvas.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(OnClickPlay);

        if (randomButton != null)
            randomButton.onClick.AddListener(OnClickRandom);

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);
    }

    void OnClickPlay()
    {
        int seed = Random.Range(0, 999999);
        StartSimulation(seed, false);
    }

    void OnClickRandom()
    {
        int seed = Random.Range(0, 999999);
        StartSimulation(seed, true);
    }

    void StartSimulation(int seed, bool useRandom)
    {
        lastUseRandom = useRandom;

        if (playButton != null) playButton.gameObject.SetActive(false);
        if (randomButton != null) randomButton.gameObject.SetActive(false);

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "Generando simulación...";
        }

        if (useRandom)
            jsonLoader.LoadWithSeedRandom(seed);
        else
            jsonLoader.LoadWithSeed(seed);

        StartCoroutine(WaitForLoad());
    }

    System.Collections.IEnumerator WaitForLoad()
    {
        float t = 0f;
        while (!jsonLoader.isLoaded && !jsonLoader.loadFailed && t < 40f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!jsonLoader.isLoaded)
        {
            if (loadingText != null)
                loadingText.text = jsonLoader.loadFailed
                    ? "No se pudo cargar la simulación."
                    : "Tardó demasiado. Reintenta.";
            if (playButton != null) playButton.gameObject.SetActive(true);
            if (randomButton != null) randomButton.gameObject.SetActive(true);
            yield break;
        }

        if (startPanel != null)
            startPanel.SetActive(false);

        if (background != null)
            background.SetActive(false);

        if (hudCanvas != null)
            hudCanvas.SetActive(true);

        if (playbackController != null)
            playbackController.StartPlayback();
    }

    public void RestartWithNewSeed()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;

        if (playbackController != null)
        {
            playbackController.ResetPlayback();
            if (playbackController.uiManager != null)
                playbackController.uiManager.HideHud();
        }

        if (startPanel != null) startPanel.SetActive(true);

        StartSimulation(Random.Range(0, 999999), lastUseRandom);
    }

    public void BackToMenu()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;

        if (playbackController != null)
        {
            playbackController.ResetPlayback();
            if (playbackController.uiManager != null)
                playbackController.uiManager.HideHud();
        }

        if (startPanel != null) startPanel.SetActive(true);
        if (background != null) background.SetActive(true);
        if (playButton != null) playButton.gameObject.SetActive(true);
        if (randomButton != null) randomButton.gameObject.SetActive(true);
        if (loadingText != null) loadingText.gameObject.SetActive(false);
    }
}
