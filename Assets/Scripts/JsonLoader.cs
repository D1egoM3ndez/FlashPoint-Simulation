// JsonLoader.cs
// Carga JSON desde el server Flask o desde StreamingAssets como fallback.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Obtiene la secuencia de estados del tablero (un <see cref="BoardData"/> por turno)
/// desde el servidor Flask de simulación y, como alternativa, desde un archivo JSON
/// local en <c>StreamingAssets</c>.
/// </summary>
/// <remarks>
/// <para>
/// Flujo: <see cref="LoadWithSeed(int)"/> / <see cref="LoadWithSeedRandom(int)"/> lanzan la
/// corrutina <see cref="LoadFromServer(int, bool)"/>; si la petición falla o no produce
/// turnos, se recurre a <see cref="LoadFromFile"/>.
/// </para>
/// <para>
/// El resultado se consulta con <see cref="GetTurn(int)"/> y <see cref="TurnCount"/>. El
/// progreso se expone mediante <see cref="isLoaded"/>, <see cref="loadFailed"/> y
/// <see cref="loadStatus"/> para que la capa de UI reaccione. Solo puede haber una carga
/// activa: cada nueva llamada cancela las corrutinas anteriores.
/// </para>
/// </remarks>
public class JsonLoader : MonoBehaviour
{
    /// <summary>URL base del servidor Flask, sin barra final (se concatena con endpoints que empiezan por <c>/</c>).</summary>
    [Header("Server Flask")]
    public string serverUrl = "http://localhost:5000";

    /// <summary>
    /// Nombre (sin extensión <c>.json</c>) del archivo de respaldo dentro de
    /// <see cref="Application.streamingAssetsPath"/>, usado cuando el servidor no responde.
    /// </summary>
    [Header("Fallback: archivo local")]
    public string fileName = "game";

    /// <summary>Turnos ya parseados y convertidos al formato interno. Vacío mientras <see cref="isLoaded"/> sea <c>false</c>.</summary>
    [HideInInspector] public List<BoardData> turns = new List<BoardData>();

    /// <summary><c>true</c> cuando la carga finalizó con al menos un turno disponible.</summary>
    [HideInInspector] public bool isLoaded = false;

    /// <summary><c>true</c> cuando la carga falló de forma irrecuperable (ni servidor ni archivo válidos).</summary>
    [HideInInspector] public bool loadFailed = false;

    /// <summary>Motivo legible del fallo; solo es significativo cuando <see cref="loadFailed"/> es <c>true</c>.</summary>
    [HideInInspector] public string loadStatus = "";

    /// <summary>Inicia una carga determinista (endpoint <c>/run-sim</c>) para la semilla dada.</summary>
    /// <param name="seed">Semilla de la simulación.</param>
    public void LoadWithSeed(int seed)       { BeginLoad(seed, false); }

    /// <summary>Inicia una carga con componente aleatorio (endpoint <c>/run-sim-rand</c>) para la semilla dada.</summary>
    /// <param name="seed">Semilla de la simulación.</param>
    public void LoadWithSeedRandom(int seed) { BeginLoad(seed, true); }

    /// <summary>
    /// Restablece el estado de carga, vacía <see cref="turns"/> y arranca la corrutina de descarga.
    /// Cancela cualquier carga anterior en curso.
    /// </summary>
    /// <param name="seed">Semilla de la simulación.</param>
    /// <param name="useRandom"><c>true</c> para el endpoint aleatorio; <c>false</c> para el determinista.</param>
    void BeginLoad(int seed, bool useRandom)
    {
        StopAllCoroutines();
        isLoaded = false;
        loadFailed = false;
        loadStatus = "";
        turns.Clear();
        StartCoroutine(LoadFromServer(seed, useRandom));
    }

    /// <summary>
    /// Solicita el JSON de la simulación al servidor Flask y lo convierte en <see cref="turns"/>.
    /// </summary>
    /// <param name="seed">Valor enviado en el parámetro de consulta <c>?seed=</c>.</param>
    /// <param name="useRandom">Selecciona el endpoint: <c>/run-sim-rand</c> si es <c>true</c>, <c>/run-sim</c> si es <c>false</c>.</param>
    /// <returns>Enumerador de corrutina de Unity.</returns>
    /// <remarks>
    /// Tiempo de espera: 30 s. Si la respuesta no es <see cref="UnityWebRequest.Result.Success"/>
    /// o el parseo produce 0 turnos, delega en <see cref="LoadFromFile"/>. En éxito deja
    /// <see cref="isLoaded"/> en <c>true</c>.
    /// </remarks>
    IEnumerator LoadFromServer(int seed, bool useRandom)
    {
        string endpoint = useRandom ? "/run-sim-rand" : "/run-sim";
        string url = $"{serverUrl}{endpoint}?seed={seed}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LoadFromFile();
                yield break;
            }

            turns = BoardDataConverter.ParseJsonArray(request.downloadHandler.text);

            if (turns.Count == 0)
            {
                LoadFromFile();
                yield break;
            }

            isLoaded = true;
        }
    }

    /// <summary>
    /// Carga y parsea el archivo JSON de respaldo <c>{fileName}.json</c> desde <c>StreamingAssets</c>.
    /// </summary>
    /// <remarks>
    /// Llama a <see cref="Fail(string)"/> si el archivo no existe o si el parseo produce 0 turnos.
    /// No captura las excepciones de E/S de <see cref="File.ReadAllText(string)"/>.
    /// </remarks>
    void LoadFromFile()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName + ".json");

        if (!File.Exists(path))
        {
            Fail($"No hay server y no existe {path}");
            return;
        }

        turns = BoardDataConverter.ParseJsonArray(File.ReadAllText(path));

        if (turns.Count == 0)
        {
            Fail($"{fileName}.json no se pudo parsear (0 turnos).");
            return;
        }

        isLoaded = true;
    }

    /// <summary>Marca la carga como fallida y guarda el motivo en <see cref="loadStatus"/>.</summary>
    /// <param name="reason">Descripción del fallo, apta para mostrar en la UI.</param>
    void Fail(string reason)
    {
        loadFailed = true;
        loadStatus = reason;
    }

    /// <summary>Devuelve el turno en el índice indicado.</summary>
    /// <param name="index">Índice del turno, en el rango <c>[0, TurnCount)</c>.</param>
    /// <returns>El <see cref="BoardData"/> correspondiente, o <c>null</c> si el índice está fuera de rango.</returns>
    public BoardData GetTurn(int index)
    {
        if (index < 0 || index >= turns.Count)
            return null;
        return turns[index];
    }

    /// <summary>Número de turnos cargados (incluye el snapshot inicial, índice 0).</summary>
    public int TurnCount => turns.Count;
}
