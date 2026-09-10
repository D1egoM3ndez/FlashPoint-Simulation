// GameManager.cs
// Control de playback: navegar entre turnos del JSON y renderizar.
// "Siguiente" reproduce el turno con animación: el bombero que actuó camina
// hasta su casilla final y, al terminar, aparecen los ghosts/zombies nuevos
// (uno por cada celda que se encendió ese turno).
using System.Collections;
using UnityEngine;

/// <summary>
/// Orquesta la reproducción de la simulación: navega por los turnos cargados por
/// <see cref="JsonLoader"/>, los envía a <see cref="BoardBuilder"/> para su render y coordina
/// la animación turno a turno y la actualización del HUD.
/// </summary>
/// <remarks>
/// <para>Ciclo de vida: <see cref="StartPlayback"/> (una sola vez) fija el turno 0 y arranca
/// el avance automático; <see cref="Update"/> avanza según <see cref="playInterval"/> salvo
/// que haya una animación en curso; <see cref="ResetPlayback"/> deja el componente listo para
/// otra simulación.</para>
/// <para>La bandera <see cref="isAnimating"/> bloquea el avance automático mientras corre
/// <see cref="AnimateTurn(int)"/>. <see cref="currentTurn"/> se confirma al destino en cuanto
/// arranca la animación, de modo que cancelarla no repite el turno.</para>
/// </remarks>
public class GameManager : MonoBehaviour
{
    /// <summary>Constructor visual del tablero. Si es <c>null</c> se busca en el mismo GameObject.</summary>
    [Header("Referencias")]
    public BoardBuilder boardBuilder;
    /// <summary>Gestor del HUD; opcional (se comprueba <c>null</c> antes de usarlo).</summary>
    public UIManager uiManager;

    /// <summary>Segundos entre avances automáticos de turno.</summary>
    [Header("Play automático")]
    public float playInterval = 1.0f;
    /// <summary>Si <c>true</c>, empieza reproduciendo al inicializar el playback.</summary>
    public bool autoPlayOnStart = false;
    /// <summary>Fuerza el arranque en reproducción aunque <see cref="autoPlayOnStart"/> sea <c>false</c>.</summary>
    [Tooltip("Arranca reproduciendo aunque autoPlayOnStart esté en false.")]
    public bool playOnLoadAlways = true;

    /// <summary>Si <c>true</c>, cada avance usa <see cref="AnimateTurn(int)"/>; si <c>false</c>, salto instantáneo.</summary>
    [Header("Animación de turno")]
    public bool animateTurns = true;
    /// <summary>Duración del desplazamiento del bombero por cada casilla.</summary>
    public float stepSeconds = 0.22f;        // por casilla que avanza el bombero
    /// <summary>Pausa tras cada paso (para que la luz/estado sea visible).</summary>
    public float afterStepSeconds = 0.15f;   // pausa entre cada paso (luz visible)
    /// <summary>Pausa al llegar el bombero a su casilla final.</summary>
    public float afterMoveSeconds = 0.25f;   // pausa al llegar a la casilla final
    /// <summary>Espera entre la aparición de cada hazard nuevo.</summary>
    public float hazardStagger = 0.14f;      // espera entre cada aparición
    /// <summary>Pausa tras aparecer todos los hazards, antes de fijar el estado final.</summary>
    public float afterHazardSeconds = 0.35f; // pausa antes de fijar el estado final

    /// <summary>Índice del turno mostrado (o confirmado como destino de la animación en curso).</summary>
    [HideInInspector] public int currentTurn = 0;
    /// <summary><c>true</c> mientras el avance automático está activo.</summary>
    [HideInInspector] public bool isPlaying = false;
    /// <summary><c>true</c> mientras <see cref="AnimateTurn(int)"/> está en ejecución.</summary>
    [HideInInspector] public bool isAnimating = false;
    /// <summary><c>true</c> una vez que hay una simulación cargada y el playback inicializado.</summary>
    [HideInInspector] public bool useJson = false;

    /// <summary>Acumulador de tiempo para el avance automático.</summary>
    float playTimer = 0f;
    /// <summary>Fuente de los turnos; se obtiene del mismo GameObject en <see cref="Start"/>.</summary>
    JsonLoader jsonLoader;
    /// <summary>Evita reinicializar el playback en llamadas repetidas a <see cref="StartPlayback"/>.</summary>
    bool initialized = false;
    /// <summary>Referencia a la corrutina de animación de turno en curso, o <c>null</c>.</summary>
    Coroutine animCo;   // animación de turno en curso (si la hay)

    /// <summary>Resuelve las referencias a <see cref="JsonLoader"/> y <see cref="BoardBuilder"/> del GameObject.</summary>
    void Start()
    {
        jsonLoader = GetComponent<JsonLoader>();
        if (boardBuilder == null)
            boardBuilder = GetComponent<BoardBuilder>();
    }

    /// <summary>
    /// Inicializa el playback: muestra el HUD, renderiza el turno 0, encuadra la cámara y,
    /// según configuración, arranca la reproducción.
    /// </summary>
    /// <remarks>No hace nada si ya se inicializó o si <see cref="JsonLoader"/> no tiene datos cargados.</remarks>
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

    /// <summary>Avanza el temporizador y dispara <see cref="NextTurn"/> al alcanzar <see cref="playInterval"/>.</summary>
    /// <remarks>Inactivo si no se ha inicializado, si no hay reproducción activa o si hay una animación en curso.</remarks>
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

    /// <summary>Renderiza el turno <paramref name="index"/> de inmediato (sin animación) y actualiza el HUD.</summary>
    /// <param name="index">Índice del turno; si está fuera de rango, no hace nada.</param>
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

    /// <summary>
    /// Detiene la animación de turno en curso, si la hay, y deja el tablero en el estado final
    /// del turno destino (<see cref="currentTurn"/>).
    /// </summary>
    /// <remarks>
    /// Necesario porque la corrutina no fija el estado final si se interrumpe: sin esto,
    /// navegar durante una animación dejaba <see cref="isAnimating"/> en <c>true</c> y la
    /// reproducción bloqueada.
    /// </remarks>
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

    /// <summary>Avanza al siguiente turno. Si se supera el último, detiene la reproducción.</summary>
    /// <remarks>
    /// Con <see cref="animateTurns"/> activo y el juego sin pausar (<see cref="Time.timeScale"/> &gt; 0)
    /// lanza <see cref="AnimateTurn(int)"/> y confirma <see cref="currentTurn"/> al destino de
    /// inmediato; en caso contrario hace un salto instantáneo con <see cref="LoadTurn(int)"/>.
    /// </remarks>
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

    /// <summary>Retrocede un turno (con salto instantáneo). No hace nada en el turno 0.</summary>
    public void PrevTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        if (currentTurn > 0)
            LoadTurn(currentTurn - 1);
    }

    /// <summary>Salta al primer turno (índice 0).</summary>
    public void FirstTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        LoadTurn(0);
    }

    /// <summary>Salta al último turno cargado.</summary>
    public void LastTurn()
    {
        if (!useJson) return;
        CancelAnimation();
        LoadTurn(jsonLoader.TurnCount - 1);
    }

    /// <summary>
    /// Detiene la reproducción y restablece el estado interno para poder volver a
    /// <see cref="StartPlayback"/> con otra simulación o regresar a la pantalla de inicio.
    /// </summary>
    // Deja el playback en cero para poder volver a arrancar (StartPlayback) con
    // otra simulación, o volver a la pantalla de inicio.
    public void ResetPlayback()
    {
        CancelAnimation();
        Stop();
        initialized = false;
        useJson = false;
        currentTurn = 0;
    }

    /// <summary>Activa el avance automático desde el turno actual.</summary>
    public void Play()
    {
        isPlaying = true;
        playTimer = 0f;
    }

    /// <summary>Detiene el avance automático.</summary>
    public void Stop()
    {
        isPlaying = false;
        playTimer = 0f;
    }

    /// <summary>Alterna entre <see cref="Play"/> y <see cref="Stop"/>.</summary>
    public void TogglePlay()
    {
        if (isPlaying) Stop(); else Play();
    }

    // ── Botones UI (onclick) ──

    // Las flechas navegan pero NO pausan: si la simulación venía corriendo,
    // sigue corriendo desde el turno al que saltaste. Se resetea el timer para
    // dar un intervalo completo de margen antes del siguiente avance automático.

    /// <summary>Handler de UI: ir al primer turno sin pausar la reproducción.</summary>
    public void OnClickFirst()  { FirstTurn(); playTimer = 0f; }
    /// <summary>Handler de UI: turno anterior sin pausar la reproducción.</summary>
    public void OnClickPrev()   { PrevTurn();  playTimer = 0f; }
    /// <summary>Handler de UI: turno siguiente sin pausar la reproducción.</summary>
    public void OnClickNext()   { NextTurn();  playTimer = 0f; }
    /// <summary>Handler de UI: ir al último turno sin pausar la reproducción.</summary>
    public void OnClickLast()   { LastTurn();  playTimer = 0f; }

    /// <summary>
    /// Handler del botón central del HUD: abre el menú de pausa si existe un
    /// <see cref="PauseMenu"/> en la escena; si no, alterna la reproducción.
    /// </summary>
    // El botón central del HUD (antes "play") ahora abre el menú de pausa.
    public void OnClickPlay()
    {
        PauseMenu pm = FindAnyObjectByType<PauseMenu>();
        if (pm != null) pm.TogglePause();
        else TogglePlay();
    }

    /// <summary>Etiqueta "Turno X / total" para el HUD (o <c>"—"</c> si no hay simulación).</summary>
    /// <returns>Texto listo para mostrar; el total es <c>TurnCount - 1</c> porque el snapshot 0 es el estado inicial.</returns>
    public string GetTurnLabel()
    {
        if (!useJson) return "—";
        // currentTurn == índice del snapshot == data.turn. El snapshot 0 es el
        // estado inicial, así que los turnos reales son TurnCount - 1.
        int total = Mathf.Max(0, jsonLoader.TurnCount - 1);
        return $"Turno {currentTurn} / {total}";
    }

    // ── Reproducción animada de un turno ──

    /// <summary>
    /// Reproduce un turno con animación: (1) el bombero que actuó camina paso a paso hasta su
    /// casilla final; (2) aparecen escalonadamente los hazards nuevos (celdas que pasaron a
    /// FIRE/SMOKE respecto al turno anterior); (3) se fija el estado autoritativo del turno con
    /// <see cref="BoardBuilder.Render(BoardData)"/> y se actualiza el HUD.
    /// </summary>
    /// <param name="index">Índice del turno a reproducir.</param>
    /// <returns>Enumerador de corrutina.</returns>
    /// <remarks>
    /// El bloque <c>try/finally</c> garantiza que <see cref="isAnimating"/> vuelve a <c>false</c>
    /// y <see cref="animCo"/> a <c>null</c> aunque la corrutina termine por <c>yield break</c>,
    /// por <see cref="MonoBehaviour.StopCoroutine(Coroutine)"/> o por excepción.
    /// Complejidad dominada por O(pasos del actor) + O(height·width) del diff de hazards.
    /// </remarks>
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

        // Turno y "Agente N" se actualizan YA, al arrancar la animación (el resto
        // del HUD —stats, tarjetas— se fija al final del turno).
        if (uiManager != null) uiManager.SetTurnInfo(cur);

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
