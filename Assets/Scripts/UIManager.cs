// UIManager.cs
// HUD por código: franja superior con el turno actual (tipografía del antiguo
// título) + tres estadísticas, y la botonera de playback abajo. Sin paneles de
// fondo; los botones llevan un relleno muy tenue. Para que el texto se lea sobre
// la escena 3D se le aplica contorno + sombra suave por material.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("HUD Texts (asignados en la escena)")]
    public TMP_Text turnText;
    public TMP_Text damageText;      // se reutiliza como valor de "Corrupción"
    public TMP_Text rescuedText;
    public TMP_Text lostText;
    public TMP_Text statusText;
    public TMP_Text playbackLabel;

    [Header("Playback")]
    public GameManager playback;

    [Header("Auto-layout")]
    [Tooltip("Reconstruye el HUD al arrancar (ignora las posiciones de la escena).")]
    public bool autoLayout = true;

    [Header("Objetivos")]
    public int soulsToRescue = 7;
    public int soulsLostLimit = 4;
    public int corruptionMax = 24;

    [Header("Legibilidad del texto")]
    [Range(0f, 0.5f)] public float textOutline = 0.18f;
    public bool textShadow = true;

    // ── Paleta (solo texto) ──
    static readonly Color LogoGreen = new Color(0.47f, 0.92f, 0.40f, 1f);
    static readonly Color CyanC     = new Color(0.40f, 0.95f, 0.88f, 1f);
    static readonly Color LabelDim  = new Color(0.78f, 0.74f, 0.90f, 1f);
    static readonly Color ValueTxt  = new Color(1f, 1f, 1f, 1f);
    static readonly Color GoldC     = new Color(1f, 0.82f, 0.35f, 1f);
    static readonly Color GreenC    = new Color(0.48f, 0.90f, 0.55f, 1f);
    static readonly Color RedC      = new Color(0.97f, 0.44f, 0.38f, 1f);
    static readonly Color BtnBg     = new Color(0.10f, 0.09f, 0.16f, 0.62f);  // fondo de botón de playback
    static readonly Color HudPill   = new Color(0.02f, 0.02f, 0.05f, 0.92f);  // fondo detrás de los textos del HUD
    static readonly Color BtnAccent = new Color(0.30f, 0.24f, 0.46f, 0.95f);  // botón central (menú de pausa)
    static readonly Color PurpleBtn   = new Color(0.22f, 0.19f, 0.33f, 1f);   // relleno botones de pausa
    static readonly Color PurpleBtnHi = new Color(0.33f, 0.27f, 0.48f, 1f);   // relleno del botón principal (Reanudar)
    static readonly Color PurpleEdge  = new Color(0.47f, 0.40f, 0.66f, 1f);   // borde morado de los botones de pausa

    RectTransform barRT;
    RectTransform hudRoot;   // contiene todo el HUD; oculto hasta ShowHud()
    TMP_Text agentText;      // "Agente N": bombero que se mueve este turno

    // Menús de estado (pausa / victoria / derrota): solo título + botones.
    GameObject victoryPanel, defeatPanel;

#if UNITY_EDITOR
    [Header("Preview de tarjetas (solo editor)")]
    [Tooltip("F9 / F10 en Play alternan la vista previa de victoria / derrota.")]
    public bool previewVictory;
    public bool previewDefeat;
#endif

    void Awake()
    {
        if (autoLayout)
            BuildLayout();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))  previewVictory = !previewVictory;
        if (Input.GetKeyDown(KeyCode.F10)) previewDefeat  = !previewDefeat;

        if (victoryPanel != null && previewVictory && !victoryPanel.activeSelf)
            victoryPanel.SetActive(true);
        if (defeatPanel != null && previewDefeat && !defeatPanel.activeSelf)
            defeatPanel.SetActive(true);
    }
#endif

    // ─────────────────────────── HUD update ───────────────────────────

    public void UpdateHUD(BoardData data)
    {
        SetTurnInfo(data);

        if (rescuedText != null)
            rescuedText.text = $"{data.victimsRescued} / {soulsToRescue}";

        if (damageText != null)
            damageText.text = $"{CorruptionPct(data)}%";

        if (lostText != null)
            lostText.text = $"{data.victimsLost} / {soulsLostLimit}";

        // El estado ganó/perdió ahora se muestra en la tarjeta de victoria/derrota.
        if (statusText != null)
            statusText.text = "";

        UpdateStateCards(data);
        UpdatePlaybackLabel();
    }

    // Solo el turno y el "Agente N": GameManager lo llama al ARRANCAR la
    // animación del turno para que no queden un paso atrás.
    public void SetTurnInfo(BoardData data)
    {
        if (turnText != null)
            turnText.text = playback != null ? playback.GetTurnLabel() : $"Turno {data.turn}";

        if (agentText != null)
            agentText.text = data.actor != null ? $"Agente {data.actor.id}" : "";
    }

    public void UpdatePlaybackLabel()
    {
        if (playbackLabel != null && playback != null)
            playbackLabel.text = playback.GetTurnLabel();
    }

    // ─────────────────────────── Layout ───────────────────────────

    void BuildLayout()
    {
        RectTransform canvas = TopCanvasRect(transform);
        if (canvas == null && turnText != null) canvas = TopCanvasRect(turnText.transform);
        if (canvas == null && statusText != null) canvas = TopCanvasRect(statusText.transform);
        if (canvas == null) return;

        // Raíz del HUD: se arma todo dentro y arranca oculto (la pantalla de
        // inicio lo enciende con ShowHud() cuando empieza la simulación).
        hudRoot = NewRect("HUD_Root", canvas);
        hudRoot.anchorMin = Vector2.zero;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;

        // contenedor de la franja superior (sin relleno propio; los fondos van
        // solo detrás del turno y del grupo de stats, ver BarPill más abajo)
        barRT = NewRect("HUD_TopBar", hudRoot);
        barRT.anchorMin = new Vector2(0f, 1f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.sizeDelta = new Vector2(-24f, 96f);
        barRT.anchoredPosition = new Vector2(0f, -6f);

        // ---- turno (izquierda, con degradado y brillo) + agente que se mueve ----
        if (turnText != null)
        {
            Reparent(turnText, barRT);
            Place(turnText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -6f), new Vector2(300f, 42f));
            StyleText(turnText, 34f, LogoGreen, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            turnText.characterSpacing = 3f;
            ApplyGradient(turnText, LogoGreen, CyanC);
            ApplyGlow(turnText, LogoGreen, 0.4f);

            agentText = NewText("HUD_Agent", barRT, "", 15f, CyanC,
                                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            agentText.characterSpacing = 2f;
            Place(agentText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -50f), new Vector2(260f, 20f));

            BarPill(new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(274f, 72f));
        }

        // ---- estadísticas: bloques anclados a la derecha (no chocan con el turno) ----
        Stat(16f,  "Perdidas",   lostText,    RedC);
        Stat(188f, "Corrupción", damageText,  GoldC);
        Stat(360f, "Rescatadas", rescuedText, GreenC);
        BarPill(new Vector2(1f, 1f), new Vector2(-8f, -4f), new Vector2(527f, 66f));

        // ---- estado (bajo la franja, centrado) ----
        if (statusText != null)
        {
            Reparent(statusText, hudRoot);
            Place(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -112f), new Vector2(440f, 40f));
            StyleText(statusText, 24f, ValueTxt, TextAlignmentOptions.Center, FontStyles.Bold);
            statusText.text = "";
        }

        // ---- barra de playback (abajo) ----
        LayoutPlaybackBar();
        RectTransform pbBar = playbackLabel != null ? playbackLabel.transform.parent as RectTransform : null;
        if (pbBar != null && pbBar != hudRoot)
            pbBar.SetParent(hudRoot, false);

        // menú de pausa: separar los botones y ponerlo por encima del HUD
        PauseMenu pm = FindAnyObjectByType<PauseMenu>();
        if (pm != null && pm.pausePanel != null)
        {
            LayoutPausePanel(pm);
            pm.pausePanel.transform.SetAsLastSibling();
        }

        BuildEndScreens(canvas, pm);

        hudRoot.gameObject.SetActive(false);
    }

    // La pantalla de inicio llama esto cuando arranca la simulación.
    public void ShowHud()
    {
        if (hudRoot != null) hudRoot.gameObject.SetActive(true);
    }

    // Oculta el HUD y las tarjetas de victoria/derrota (volver al menú / reiniciar).
    public void HideHud()
    {
        if (hudRoot != null) hudRoot.gameObject.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    // ───────────────────────── Tarjetas de estado ─────────────────────────
    // Pausa (dorado), victoria (verde) y derrota (rojo) comparten el mismo marco:
    // borde de color pegado al canto + escuadras en las esquinas. Todo por código.

    void LayoutPausePanel(PauseMenu pm)
    {
        RectTransform panel = pm.pausePanel.transform as RectTransform;
        if (panel == null) return;

        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        RectTransform veil = NewRect("PauseVeil", panel);
        Image vimg = veil.gameObject.AddComponent<Image>();
        vimg.color = new Color(0.02f, 0.02f, 0.05f, 0.84f);
        vimg.raycastTarget = true;
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.offsetMin = Vector2.zero;
        veil.offsetMax = Vector2.zero;
        veil.SetAsFirstSibling();

        Vector2 size = new Vector2(360f, 236f);
        RectTransform card = BuildCardBase(panel, "PauseCard", size, GoldC, "PAUSA", out float y);

        Button[] all = panel.GetComponentsInChildren<Button>(true);
        Button cont = null, rest = null, quit = null;
        foreach (Button b in all)
        {
            string m = PersistentMethod(b);
            if (m.Contains("Continue")) cont = b;
            else if (m.Contains("Restart")) rest = b;
            else if (m.Contains("Quit")) quit = b;
        }
        if (cont == null && rest == null && quit == null && all.Length >= 3)
        {
            System.Array.Sort(all,
                (x, z) => x.transform.GetSiblingIndex().CompareTo(z.transform.GetSiblingIndex()));
            cont = all[0]; rest = all[1]; quit = all[2];
        }

        PauseEntry(cont, "Reanudar", card, y, true);
        y -= 52f;
        PauseEntry(rest, "Reiniciar", card, y, false);
        y -= 52f;
        PauseEntry(quit, "Salir", card, y, false);
    }

    void PauseEntry(Button b, string label, Transform parent, float y, bool primary)
    {
        if (b == null) return;
        if (parent != null && b.transform.parent != parent)
            b.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)b.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(244f, 44f);
        rt.anchoredPosition = new Vector2(0f, y);

        // Los tres botones con el mismo morado claro.
        Image img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = RoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = PurpleEdge;
            img.raycastTarget = true;
        }
        b.targetGraphic = img;

        RectTransform fill = b.transform.Find("BtnFill") as RectTransform;
        if (fill == null)
        {
            fill = NewRect("BtnFill", b.transform);
            fill.gameObject.AddComponent<Image>();
        }
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        fill.SetAsFirstSibling();
        Image fimg = fill.GetComponent<Image>();
        fimg.sprite = RoundedSprite();
        fimg.type = Image.Type.Sliced;
        fimg.color = PurpleBtnHi;
        fimg.raycastTarget = false;

        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.transform.SetAsLastSibling();
            t.text = label.ToUpperInvariant();
            RectTransform trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            StyleText(t, primary ? 16f : 15f, Color.white,
                      TextAlignmentOptions.Center, FontStyles.Bold);
            t.characterSpacing = 4f;
        }
    }

    void BuildEndScreens(RectTransform canvas, PauseMenu pm)
    {
        if (canvas == null) return;
        StartScreen ss = FindAnyObjectByType<StartScreen>();
        victoryPanel = BuildEndCard(canvas, "VictoryPanel", GreenC, "TURNO COMPLETO", "Nueva partida", pm, ss);
        defeatPanel = BuildEndCard(canvas, "DefeatPanel", RedC, "TURNO FALLIDO", "Reintentar", pm, ss);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    GameObject BuildEndCard(Transform canvas, string name, Color accent, string title,
                            string mainLabel, PauseMenu pm, StartScreen ss)
    {
        RectTransform panel = NewRect(name, canvas);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        Image veil = panel.gameObject.AddComponent<Image>();
        veil.color = new Color(0.02f, 0.02f, 0.05f, 0.88f);
        veil.raycastTarget = true;

        Vector2 size = new Vector2(340f, 190f);
        RectTransform card = BuildCardBase(panel, name + "_Card", size, accent, title, out float y);

        // Botón de arriba: reiniciar la simulación con una seed nueva.
        Button main = MakeCardButton(card, mainLabel, accent, true);
        Place((RectTransform)main.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              new Vector2(0f, y), new Vector2(238f, 46f));
        main.onClick.AddListener(() =>
        {
            if (ss != null) ss.RestartWithNewSeed();
            else if (pm != null) pm.OnClickRestart();
        });
        y -= 56f;

        // Botón de abajo: volver a la pantalla principal.
        Button exit = MakeCardButton(card, "Salir", new Color(0.62f, 0.58f, 0.72f, 1f), false);
        Place((RectTransform)exit.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              new Vector2(0f, y), new Vector2(160f, 38f));
        exit.onClick.AddListener(() =>
        {
            if (ss != null) ss.BackToMenu();
            else if (pm != null) pm.OnClickQuit();
        });

        return panel.gameObject;
    }

    // Tarjeta con marco decorativo + título + separador. Devuelve el RectTransform
    // de la tarjeta y, en `contentTop`, la Y (relativa al borde superior) donde
    // empezar a apilar el contenido.
    RectTransform BuildCardBase(Transform parent, string name, Vector2 size, Color accent,
                                string title, out float contentTop)
    {
        RectTransform card = NewRect(name, parent);
        Image cimg = card.gameObject.AddComponent<Image>();
        cimg.sprite = RoundedSprite();
        cimg.type = Image.Type.Sliced;
        cimg.color = new Color(0.11f, 0.09f, 0.16f, 0.985f);
        cimg.raycastTarget = true;
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

        ThemedFrame(card, accent);

        TMP_Text t = NewText(name + "_Title", card, title, 25f, accent,
                             TextAlignmentOptions.Center, FontStyles.Bold);
        t.characterSpacing = 7f;
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
              new Vector2(0f, -16f), new Vector2(size.x - 64f, 34f));
        ApplyGlow(t, accent, 0.4f);

        Image div = MakeLine(card, new Color(accent.r, accent.g, accent.b, 0.5f));
        RectTransform drt = div.rectTransform;
        drt.anchorMin = new Vector2(0.5f, 1f);
        drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(size.x * 0.5f, 2f);
        drt.anchoredPosition = new Vector2(0f, -52f);

        contentTop = -68f;
        return card;
    }

    // Borde: un solo color, 4 líneas pegadas al canto de la tarjeta. Sin esquinas.
    void ThemedFrame(RectTransform card, Color accent)
    {
        const float thick = 2f;
        Color c = new Color(accent.r, accent.g, accent.b, 1f);

        Edge(card, c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thick));
        Edge(card, c, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thick));
        Edge(card, c, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thick, 0f));
        Edge(card, c, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thick, 0f));
    }

    void Edge(RectTransform card, Color c, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 sizeDelta)
    {
        RectTransform rt = MakeLine(card, c).rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = piv;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
    }

    Image MakeLine(Transform parent, Color c)
    {
        RectTransform rt = NewRect("FrameLine", parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    // `accent` se ignora: los botones de victoria/derrota usan el mismo morado
    // que los del menú de pausa. `solid` solo cambia el tamaño de la fuente.
    Button MakeCardButton(Transform parent, string label, Color accent, bool solid)
    {
        RectTransform rt = NewRect("Btn_" + label, parent);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = RoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = PurpleEdge;
        bg.raycastTarget = true;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;

        RectTransform inner = NewRect("BtnFill", rt);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(2f, 2f);
        inner.offsetMax = new Vector2(-2f, -2f);
        Image fi = inner.gameObject.AddComponent<Image>();
        fi.sprite = RoundedSprite();
        fi.type = Image.Type.Sliced;
        fi.color = PurpleBtnHi;
        fi.raycastTarget = false;

        TMP_Text t = NewText("Btn_" + label + "_T", inner, label.ToUpperInvariant(),
                             solid ? 16f : 15f, Color.white,
                             TextAlignmentOptions.Center, FontStyles.Bold);
        t.characterSpacing = 4f;
        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return b;
    }

    int CorruptionPct(BoardData data)
    {
        int max = Mathf.Max(1, corruptionMax);
        return Mathf.RoundToInt(Mathf.Clamp01(data.damageTotal / (float)max) * 100f);
    }

    void UpdateStateCards(BoardData data)
    {
        bool won = data.status == "won";
        bool lost = data.status == "lost";
#if UNITY_EDITOR
        if (previewVictory) won = true;
        if (previewDefeat) lost = true;
#endif

        if (victoryPanel != null)
        {
            if (victoryPanel.activeSelf != won) victoryPanel.SetActive(won);
            if (won) victoryPanel.transform.SetAsLastSibling();
        }
        if (defeatPanel != null)
        {
            if (defeatPanel.activeSelf != lost) defeatPanel.SetActive(lost);
            if (lost) defeatPanel.transform.SetAsLastSibling();
        }
    }

    static string PersistentMethod(Button b)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
        {
            string m = b.onClick.GetPersistentMethodName(i);
            if (!string.IsNullOrEmpty(m)) return m;
        }
        return "";
    }

    // Fondo redondeado detrás de un grupo de textos del HUD (mismo color que los
    // botones de la barra de playback). Se manda al fondo para no tapar el texto.
    void BarPill(Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect("HUD_Pill", barRT);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = RoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = HudPill;
        img.raycastTarget = false;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.SetAsFirstSibling();
    }

    // Bloque de estadística anclado al borde derecho de la barra.
    // rightGap = distancia (px) del borde derecho de la barra al borde derecho del bloque.
    void Stat(float rightGap, string label, TMP_Text valueText, Color valueColor)
    {
        const float blockW = 165f;

        RectTransform block = NewRect("HUD_Stat_" + label, barRT);
        block.anchorMin = new Vector2(1f, 1f);
        block.anchorMax = new Vector2(1f, 1f);
        block.pivot = new Vector2(1f, 1f);
        block.sizeDelta = new Vector2(blockW, 60f);
        block.anchoredPosition = new Vector2(-rightGap, -12f);

        TMP_Text lbl = NewText("HUD_Lbl_" + label, block, label.ToUpperInvariant(), 12f, LabelDim,
                            TextAlignmentOptions.TopLeft, FontStyles.Normal);
        lbl.characterSpacing = 3f;
        Place(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(blockW, 16f));

        if (valueText != null)
        {
            Reparent(valueText, block);
            Place(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -20f), new Vector2(blockW, 34f));
            StyleText(valueText, 26f, valueColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            valueText.characterSpacing = 1f;
            ApplyGlow(valueText, valueColor, 0.3f);
        }
    }

    void LayoutPlaybackBar()
    {
        RectTransform bar = playbackLabel != null ? playbackLabel.transform.parent as RectTransform : null;
        if (bar == null) return;

        Image bg = bar.GetComponent<Image>();
        if (bg != null) bg.enabled = false;   // sin fondo en la barra

        // el turno ya se muestra arriba: se oculta la etiqueta de abajo
        playbackLabel.gameObject.SetActive(false);

        bar.anchorMin = new Vector2(0.5f, 0f);
        bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(560f, 58f);
        bar.anchoredPosition = new Vector2(0f, 4f);   // pegada al borde inferior

        Button[] buttons = bar.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0) return;
        System.Array.Sort(buttons,
            (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        // orden en la escena: First, Prev, [Play->Menú de pausa], Next, Last.
        // El botón central deja de ser "play": su OnClickPlay ahora abre el
        // menú de pausa (Continuar / Reiniciar / Salir).
        int mid = buttons.Length / 2;
        string[] glyphs = { "|<<", "<<", "II", ">>", ">>|" };
        const float bw = 92f, bh = 42f, bgap = 12f;
        float totalW = buttons.Length * bw + (buttons.Length - 1) * bgap;
        float startX = -totalW * 0.5f + bw * 0.5f;

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isPause = i == mid;

            RectTransform brt = (RectTransform)buttons[i].transform;
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(isPause ? bw + 6f : bw, isPause ? bh + 6f : bh);
            brt.anchoredPosition = new Vector2(startX + i * (bw + bgap), isPause ? 5f : 8f);

            Image bimg = buttons[i].GetComponent<Image>();
            if (bimg != null)
            {
                bimg.sprite = RoundedSprite();
                bimg.type = Image.Type.Sliced;
                bimg.color = isPause ? BtnAccent : BtnBg;
                bimg.raycastTarget = true;
            }

            TMP_Text lbl = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (lbl != null)
            {
                if (i < glyphs.Length) lbl.text = glyphs[i];
                RectTransform trt = lbl.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                StyleText(lbl, isPause ? 24f : 22f,
                        isPause ? LogoGreen : ValueTxt,
                        TextAlignmentOptions.Center, FontStyles.Bold);

                if (isPause)
                {
                    lbl.characterSpacing = -3f;   // junta las dos "I" del glifo de pausa
                    lbl.text = "II";
                    ApplyGlow(lbl, LogoGreen, 0.35f);
                }
            }
        }
    }

    // ─────────────────────────── helpers ───────────────────────────

    // Sprite de rectángulo redondeado generado en memoria (con borde 9-slice), así
    // no dependemos de los builtin de Unity (que ya no se pueden cargar por nombre).
    static Sprite _roundedSprite;
    static Sprite RoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;

        const int s = 48, r = 12;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            name = "UIManager_RoundedRect",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var px = new Color32[s * s];
        const float half = s * 0.5f;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                           Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float dist = Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - r;
                byte a = (byte)(Mathf.Clamp01(0.5f - dist) * 255f);
                px[y * s + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f),
                                       100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        _roundedSprite.name = "UIManager_RoundedRect";
        return _roundedSprite;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    TMP_Text NewText(string name, Transform parent, string content, float size, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        RectTransform rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_Text src = turnText ?? rescuedText ?? lostText ?? damageText ?? statusText ?? playbackLabel;
        if (src != null && src.font != null) t.font = src.font;
        t.text = content;
        StyleText(t, size, color, align, style);
        return t;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void Reparent(Component c, Transform parent)
    {
        if (c != null && c.transform.parent != parent)
            c.transform.SetParent(parent, false);
    }

    static RectTransform TopCanvasRect(Transform t)
    {
        Canvas canvas = t.GetComponentInParent<Canvas>();
        return canvas == null ? null : canvas.transform as RectTransform;
    }

    void StyleText(TMP_Text t, float size, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        if (t == null) return;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.characterSpacing = 0f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        ApplyReadable(t);
    }

    // Contorno + sombra suave sobre una instancia de material (no toca el asset).
    void ApplyReadable(TMP_Text t)
    {
        if (t == null) return;
        try
        {
            Material m = t.fontMaterial;   // getter -> instancia única por texto
            if (m == null) return;

            m.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Max(0f, textOutline));
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.95f));

            if (textShadow)
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }

            t.UpdateMeshPadding();
        }
        catch { /* material aún no disponible (objeto inactivo): se ignora */ }
    }

    // Degradado vertical del texto (top -> bottom). El color de cara pasa a
    // blanco para que el degradado no se multiplique y salga apagado.
    static void ApplyGradient(TMP_Text t, Color top, Color bottom)
    {
        if (t == null) return;
        t.color = Color.white;
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(top, top, bottom, bottom);
    }

    // Resplandor de color alrededor del glifo (sin fondo).
    void ApplyGlow(TMP_Text t, Color glow, float power)
    {
        if (t == null) return;
        try
        {
            Material m = t.fontMaterial;
            if (m == null) return;
            m.EnableKeyword("GLOW_ON");
            m.SetColor(ShaderUtilities.ID_GlowColor, glow);
            m.SetFloat(ShaderUtilities.ID_GlowPower, Mathf.Clamp01(power));
            m.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
            m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
            t.UpdateMeshPadding();
        }
        catch { }
    }
}
