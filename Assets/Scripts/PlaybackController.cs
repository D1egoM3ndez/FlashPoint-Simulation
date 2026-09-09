// PlaybackController.cs
// Control de playback: navegar entre turnos del JSON y renderizar.
// "Siguiente" reproduce el turno con animación: el bombero que actuó camina
// hasta su casilla final y, al terminar, aparecen los ghosts/zombies nuevos
// (uno por cada celda que se encendió ese turno).
using System.Collections;
using UnityEngine;

public class PlaybackController : MonoBehaviour
{
    [Header("Referencias")]
    public BoardBuilder boardBuilder;
    public UIManager uiManager;

    [Header("Play automático")]
    public float playInterval = 1.0f;
    public bool autoPlayOnStart = false;
    [Tooltip("Arranca reproduciendo aunque autoPlayOnStart esté en false.")]
    public bool playOnLoadAlways = true;

    [Header("Animación de turno")]
    public bool animateTurns = true;
    public float stepSeconds = 0.22f;        // por casilla que avanza el bombero
    public float afterStepSeconds = 0.15f;   // pausa entre cada paso (luz visible)
    public float afterMoveSeconds = 0.25f;   // pausa al llegar a la casilla final
    public float hazardStagger = 0.14f;      // espera entre cada aparición
    public float afterHazardSeconds = 0.35f; // pausa antes de fijar el estado final

    [HideInInspector] public int currentTurn = 0;
    [HideInInspector] public bool isPlaying = false;
    [HideInInspector] public bool isAnimating = false;
    [HideInInspector] public bool useJson = false;

    float playTimer = 0f;
    JsonLoader jsonLoader;
    bool initialized = false;
    Coroutine animCo;   // animación de turno en curso (si la hay)

    void Start()
    {
        jsonLoader = GetComponent<JsonLoader>();
        if (boardBuilder == null)
            boardBuilder = GetComponent<BoardBuilder>();
    }

    public void StartPlayback()
    {
        if (initialized) return;
        if (jsonLoader == null || !jsonLoader.isLoaded) return;

        initialized = true;
        useJson = true;
        if (uiManager != null) uiManager.ShowHud();
        LoadTurn(0);
        boardBuilder.FitCamera(10, 8);
        if (autoPlayOnStart || playOnLoadAlways) Play();
    }

    void Update()
    {
        if (!initialized) return;

        if (!useJson || !isPlaying || isAnimating) return;

        playTimer += Time.deltaTime;
        if (playTimer >= playInterval)
        {
            playTimer = 0f;
            NextTurn();
        }
    }

    // ── Render instantáneo ──

    public void LoadTurn(int index)
    {
        if (!useJson) return;

        BoardData data = jsonLoader.GetTurn(index);
        if (data == null) return;

        currentTurn = index;
        boardBuilder.Render(data);

        if (uiManager != null)
            uiManager.UpdateHUD(data);
    }

    // ── Navegación ──

    // Corta la animación de turno en curso (si la hay) y deja el estado listo
    // para navegar. Sin esto, pulsar una flecha mientras un turno se reproduce
    // no hacía nada (isAnimating seguía en true) y, si la corrutina se cortaba
    // a medias, la simulación quedaba "pegada" para siempre.
    void CancelAnimation()
    {
        if (animCo != null)
        {
            StopCoroutine(animCo);
            animCo = null;
            isAnimating = false;
            // La corrutina no llegó a fijar el estado final del turno: lo fijamos
            // aquí para que el tablero quede consistente (sin el bombero a medio
            // paso ni humos/fuegos a medias). currentTurn ya apunta al destino
            // porque NextTurn lo confirma al arrancar la animación.
            LoadTurn(currentTurn);
        }
        isAnimating = false;
    }

    public void NextTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        if (currentTurn >= jsonLoader.TurnCount - 1)
        {
            Stop();
            return;
        }

        int target = currentTurn + 1;
        // Con el juego en pausa (Time.timeScale == 0) la corrutina se congelaría
        // a mitad del paso: en ese caso saltamos al turno de forma instantánea.
        if (animateTurns && isActiveAndEnabled && Time.timeScale > 0f)
        {
            // Confirmar el turno destino YA: la animación es solo visual. Si se
            // cancela a mitad, currentTurn no se queda atrás y "Siguiente" no
            // repite el mismo turno.
            currentTurn = target;
            animCo = StartCoroutine(AnimateTurn(target));
        }
        else
        {
            LoadTurn(target);
        }
    }

    public void PrevTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        if (currentTurn > 0)
            LoadTurn(currentTurn - 1);
    }

    public void FirstTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        LoadTurn(0);
    }

    public void LastTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        LoadTurn(jsonLoader.TurnCount - 1);
    }

    public void Play()
    {
        isPlaying = true;
        playTimer = 0f;
    }

    public void Stop()
    {
        isPlaying = false;
        playTimer = 0f;
    }

    public void TogglePlay()
    {
        if (isPlaying) Stop(); else Play();
    }

    // ── Botones UI (onclick) ──

    // Las flechas navegan pero NO pausan: si la simulación venía corriendo,
    // sigue corriendo desde el turno al que saltaste. Se resetea el timer para
    // dar un intervalo completo de margen antes del siguiente avance automático.
    public void OnClickFirst()  { FirstTurn(); playTimer = 0f; }
    public void OnClickPrev()   { PrevTurn();  playTimer = 0f; }
    public void OnClickNext()   { NextTurn();  playTimer = 0f; }
    public void OnClickLast()   { LastTurn();  playTimer = 0f; }

    // El botón central del HUD (antes "play") ahora abre el menú de pausa.
    public void OnClickPlay()
    {
        PauseMenu pm = FindAnyObjectByType<PauseMenu>();
        if (pm != null) pm.TogglePause();
        else TogglePlay();
    }

    public string GetTurnLabel()
    {
        if (!useJson) return "—";
        return $"Turno {currentTurn + 1} / {jsonLoader.TurnCount}";
    }

    // ── Reproducción animada de un turno ──

    IEnumerator AnimateTurn(int index)
    {
        isAnimating = true;
        // try/finally: pase lo que pase (yield break, StopCoroutine desde una
        // flecha, o una excepción a mitad) isAnimating vuelve a false y no se
        // bloquea la navegación.
        try
        {

        BoardData cur = jsonLoader.GetTurn(index);
        if (cur == null) yield break;

        // En pantalla sigue renderizado el turno anterior (index - 1), con cada
        // bombero en la casilla donde terminó (= casilla de salida del actor de
        // este turno). currentTurn ya vale `index` (lo confirmó NextTurn).

        // 1. El bombero que actuó camina hasta su casilla final.
        ActorData actor = cur.actor;
        if (actor != null && actor.HasPath)
        {
            GameObject go = boardBuilder.GetAgentObject(actor.id);
            if (go == null)
            {
                // Sin objeto que animar: pasar al turno de forma instantánea.
                LoadTurn(index);
                yield break;
            }

            {
                boardBuilder.ShowAgentIndicator(actor.id);

                Animator anim = go.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetBool("IsWalking", true);

                int rr = actor.startRow, cc = actor.startCol;
                go.transform.position = boardBuilder.CellWorld(rr, cc);

                float dur = Mathf.Max(0.01f, stepSeconds);
                foreach (ActorStep step in actor.steps)
                {
                    int nr = rr, nc = cc;
                    switch (step.direction)
                    {
                        case "UP": nr--; break;
                        case "DOWN": nr++; break;
                        case "LEFT": nc--; break;
                        case "RIGHT": nc++; break;
                    }

                    Vector3 from = boardBuilder.CellWorld(rr, cc);
                    Vector3 to = boardBuilder.CellWorld(nr, nc);
                    if (to != from)
                        go.transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);

                    float e = 0f;
                    while (e < dur)
                    {
                        e += Time.deltaTime;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                        go.transform.position = Vector3.Lerp(from, to, k);
                        yield return null;
                    }
                    rr = nr; cc = nc;

                    if (afterStepSeconds > 0f)
                        yield return new WaitForSeconds(afterStepSeconds);
                }

                go.transform.position = boardBuilder.CellWorld(actor.finalRow, actor.finalCol);

                if (anim != null) anim.SetBool("IsWalking", false);
            }

            if (afterMoveSeconds > 0f)
                yield return new WaitForSeconds(afterMoveSeconds);
        }

        // 2. Aparecen los ghosts/zombies nuevos: comparar el tablero
        //    anterior con el actual para detectar celdas que cambiaron
        //    a FIRE (zombie) o SMOKE (ghost).
        BoardData prev = (index > 0) ? jsonLoader.GetTurn(index - 1) : null;
        for (int r = 0; r < cur.height; r++)
        {
            for (int c = 0; c < cur.width; c++)
            {
                int idx = r * cur.width + c;
                string curState = (idx >= 0 && idx < cur.cells.Count) ? cur.cells[idx].state : "CLEAR";
                string prevState = "CLEAR";
                if (prev != null && idx >= 0 && idx < prev.cells.Count)
                    prevState = prev.cells[idx].state;

                if (curState != prevState && (curState == "FIRE" || curState == "SMOKE"))
                {
                    boardBuilder.SpawnHazardAnimated(r, c, curState);
                    if (hazardStagger > 0f)
                        yield return new WaitForSeconds(hazardStagger);
                }
            }
        }

        if (afterHazardSeconds > 0f)
            yield return new WaitForSeconds(afterHazardSeconds);

        // 3. Fijar el estado autoritativo del turno (apaga fuegos, POIs, resto de agentes).
        boardBuilder.Render(cur);
        if (cur.actor != null) boardBuilder.ShowAgentIndicator(cur.actor.id);
        currentTurn = index;
        if (uiManager != null) uiManager.UpdateHUD(cur);

        }
        finally
        {
            isAnimating = false;
            animCo = null;
        }
    }
}
