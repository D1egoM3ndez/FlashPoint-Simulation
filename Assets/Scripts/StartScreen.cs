// StartScreen.cs
// Pantalla de inicio con un botón para generar simulación.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartScreen : MonoBehaviour
{
    [Header("Referencias")]
    public JsonLoader jsonLoader;
    public PlaybackController playbackController;

    [Header("UI")]
    public GameObject startPanel;
    [Tooltip("Imagen de fondo de la portada (con el título FLASHPOINT). Se oculta al empezar.")]
    public GameObject background;
    public Button playButton;
    public TMP_Text loadingText;

    void Start()
    {
        if (background != null)
            background.SetActive(true);

        if (startPanel != null)
            startPanel.SetActive(true);

        if (playButton != null)
            playButton.onClick.AddListener(OnClickPlay);

        if (loadingText != null)
            loadingText.gameObject.SetActive(false);
    }

    void OnClickPlay()
    {
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = "Generando simulación...";
        }

        int seed = Random.Range(0, 999999);
        jsonLoader.LoadWithSeed(seed);
        StartCoroutine(WaitForLoad());
    }

    System.Collections.IEnumerator WaitForLoad()
    {
        while (!jsonLoader.isLoaded)
            yield return null;

        if (startPanel != null)
            startPanel.SetActive(false);

        if (background != null)
            background.SetActive(false);

        if (playbackController != null)
            playbackController.StartPlayback();   // esto enciende el HUD (UIManager.ShowHud)
    }
}
