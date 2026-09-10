// BoardBuilder.cs
// Instancia el tablero en la escena a partir de un BoardData (JSON del server).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye la representación visual del tablero en la escena a partir de un
/// <see cref="BoardData"/>: pisos, muros, puertas, fuego/humo, POIs y agentes.
/// </summary>
/// <remarks>
/// <para><see cref="Render(BoardData)"/> reconstruye el tablero completo en cada llamada
/// (destruye y vuelve a instanciar todos los objetos). <see cref="GameManager"/> lo invoca
/// una vez por turno y usa además <see cref="SpawnHazardAnimated(int, int, string)"/> y
/// <see cref="CellWorld(int, int)"/> para animar transiciones.</para>
/// <para>Convención de coordenadas: columna → eje X, fila → eje Z negativo, con el origen en
/// la celda (0,0).</para>
/// </remarks>
public class BoardBuilder : MonoBehaviour
{
    /// <summary>Longitud del lado de cada celda en unidades de mundo.</summary>
    public float cellSize = 2f;

    /// <summary>Duración de la animación de aparición (<see cref="PopIn"/>) de fuego/humo.</summary>
    [Header("Animación")]
    public float hazardPopTime = 0.25f;

    /// <summary>Mapa id de agente → GameObject instanciado. Se reconstruye en cada <see cref="Render(BoardData)"/>.</summary>
    readonly Dictionary<int, GameObject> agentById = new Dictionary<int, GameObject>();

    /// <summary>Prefab de piso para celdas interiores.</summary>
    [Header("Prefabs")]
    public GameObject floorInsidePrefab;
    /// <summary>Prefab de piso para celdas del perímetro.</summary>
    public GameObject floorOutsidePrefab;
    public GameObject wallPrefab;
    public GameObject doorOpenPrefab;
    public GameObject doorClosedPrefab;
    /// <summary>Prefab para celdas en estado <c>"FIRE"</c>.</summary>
    public GameObject zombiePrefab;
    /// <summary>Prefab para celdas en estado <c>"SMOKE"</c>.</summary>
    public GameObject ghostPrefab;
    public GameObject poiUnknownPrefab;
    public GameObject poiVictimPrefab;
    public GameObject poiFalsePrefab;
    public GameObject agentPrefab;
    /// <summary>Efecto de partículas opcional al aparecer un hazard; se autodestruye a 1 s.</summary>
    public GameObject sparkPrefab;

    /// <summary>Contenedores (padres de jerarquía) donde se agrupan los objetos instanciados por tipo.</summary>
    [Header("Contenedores")]
    public Transform floorParent;
    public Transform wallsParent;
    public Transform doorsParent;
    public Transform fireParent;
    public Transform smokeParent;
    public Transform poisParent;
    public Transform agentsParent;

    /// <summary>Evita que <see cref="FitCamera(int, int)"/> reencuadre más de una vez.</summary>
    bool cameraFitted = false;

    /// <summary>
    /// Ajusta la cámara top-down para que el tablero quepa en pantalla. Solo surte efecto la
    /// primera vez que se llama.
    /// </summary>
    /// <param name="width">Ancho del tablero en celdas.</param>
    /// <param name="height">Alto del tablero en celdas.</param>
    /// <remarks>Requiere una <see cref="Camera.main"/> con un componente <c>TopDownCamera</c>; si no existe el componente, no hace nada.</remarks>
    public void FitCamera(int width, int height)
    {
        if (cameraFitted) return;
        cameraFitted = true;
        TopDownCamera cam = Camera.main.GetComponent<TopDownCamera>();
        if (cam != null)
            cam.FitToBoard(width, height, cellSize);
    }

    /// <summary>Centro en coordenadas de mundo de la celda (<paramref name="row"/>, <paramref name="col"/>), a altura 0.</summary>
    /// <param name="row">Fila (crece hacia -Z).</param>
    /// <param name="col">Columna (crece hacia +X).</param>
    /// <returns>Posición del centro de la celda.</returns>
    Vector3 CellCenter(int row, int col)
    {
        return new Vector3(col * cellSize, 0f, -row * cellSize);
    }

    /// <summary>Indica si la celda está en el perímetro del tablero.</summary>
    /// <returns><c>true</c> si la celda pertenece a la primera/última fila o columna.</returns>
    bool IsOutside(int row, int col, int height, int width)
    {
        return row == 0 || row == height - 1 || col == 0 || col == width - 1;
    }

    /// <summary>
    /// Reconstruye por completo el tablero para el turno indicado: limpia todos los
    /// contenedores e instancia piso, bordes, hazards, POIs y agentes.
    /// </summary>
    /// <param name="data">Estado del turno a representar.</param>
    /// <remarks>
    /// Complejidad O(celdas·4 + pois + agentes) instanciaciones por llamada, más el
    /// <see cref="Object.Destroy(Object)"/> de todo lo anterior. Rellena <see cref="agentById"/>.
    /// </remarks>
    public void Render(BoardData data)
    {
        Clear(floorParent);
        Clear(wallsParent);
        Clear(doorsParent);
        Clear(fireParent);
        Clear(smokeParent);
        Clear(poisParent);
        Clear(agentsParent);

        foreach (CellData cell in data.cells)
        {
            Vector3 center = CellCenter(cell.row, cell.col);
            bool outside = IsOutside(cell.row, cell.col, data.height, data.width);

            GameObject floorPrefab = outside ? floorOutsidePrefab : floorInsidePrefab;
            Instantiate(floorPrefab, center, Quaternion.identity, floorParent);

            PlaceEdge(cell.row, cell.col, "Up", cell.wallUp, center);
            PlaceEdge(cell.row, cell.col, "Left", cell.wallLeft, center);
            PlaceEdge(cell.row, cell.col, "Down", cell.wallDown, center);
            PlaceEdge(cell.row, cell.col, "Right", cell.wallRight, center);

            if (cell.state == "FIRE")
                Instantiate(zombiePrefab, center + Vector3.up * 0.3f, Quaternion.identity, fireParent);
            else if (cell.state == "SMOKE")
                Instantiate(ghostPrefab, center + Vector3.up * 0.3f, Quaternion.identity, smokeParent);
        }

        foreach (PoiData poi in data.pois)
        {
            Vector3 center = CellCenter(poi.row, poi.col) + Vector3.up * 0.5f;
            GameObject prefab = poiUnknownPrefab;
            if (poi.revealed)
                prefab = poi.kind == "VICTIM" ? poiVictimPrefab : poiFalsePrefab;
            Instantiate(prefab, center, Quaternion.identity, poisParent);
        }

        agentById.Clear();
        foreach (AgentData agent in data.agents)
        {
            Vector3 center = CellCenter(agent.row, agent.col) + Vector3.up * 0.5f;
            GameObject go = Instantiate(agentPrefab, center, Quaternion.identity, agentsParent);
            agentById[agent.id] = go;
        }
    }

    /// <summary>Posición de mundo del centro de una celda, elevada 0.5 para colocar actores encima del piso.</summary>
    /// <param name="row">Fila.</param>
    /// <param name="col">Columna.</param>
    /// <returns>Punto sobre el que situar/animar un agente.</returns>
    public Vector3 CellWorld(int row, int col)
    {
        return CellCenter(row, col) + Vector3.up * 0.5f;
    }

    /// <summary>Devuelve el GameObject del agente con el id dado en el turno renderizado.</summary>
    /// <param name="id">Id del agente.</param>
    /// <returns>El GameObject, o <c>null</c> si no existe en el turno actual.</returns>
    public GameObject GetAgentObject(int id)
    {
        return agentById.TryGetValue(id, out GameObject go) ? go : null;
    }

    /// <summary>Muestra el indicador de turno sobre el agente indicado y lo oculta en el resto.</summary>
    /// <param name="agentId">Id del agente a resaltar.</param>
    /// <remarks>Busca un hijo llamado <c>"Turno"</c> en el prefab del agente; si no existe, no hace nada.</remarks>
    public void ShowAgentIndicator(int agentId)
    {
        HideAllIndicators();
        if (!agentById.TryGetValue(agentId, out GameObject go)) return;
        Transform indicator = go.transform.Find("Turno");
        if (indicator != null) indicator.gameObject.SetActive(true);
    }

    /// <summary>Oculta el indicador de turno en todos los agentes del turno actual.</summary>
    public void HideAllIndicators()
    {
        foreach (var go in agentById.Values)
        {
            Transform indicator = go.transform.Find("Turno");
            if (indicator != null) indicator.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Instancia fuego (<c>"FIRE"</c>) o humo (<c>"SMOKE"</c>) en una celda con animación de
    /// aparición y, si está configurado, un efecto de chispa temporal.
    /// </summary>
    /// <param name="row">Fila de la celda.</param>
    /// <param name="col">Columna de la celda.</param>
    /// <param name="state">Estado a representar: <c>"SMOKE"</c>; cualquier otro valor se trata como fuego.</param>
    /// <remarks>No hace nada si el prefab correspondiente es <c>null</c>. El objeto se añade al contenedor de humo o de fuego, no a <see cref="agentById"/>.</remarks>
    public void SpawnHazardAnimated(int row, int col, string state)
    {
        bool smoke = state == "SMOKE";
        GameObject prefab = smoke ? ghostPrefab : zombiePrefab;
        Transform parent = smoke ? smokeParent : fireParent;
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, CellCenter(row, col) + Vector3.up * 0.3f,
                                    Quaternion.identity, parent);
        StartCoroutine(PopIn(go.transform, hazardPopTime));

        if (sparkPrefab != null)
        {
            GameObject spark = Instantiate(sparkPrefab, go.transform.position, Quaternion.identity);
            Destroy(spark, 1f);
        }
    }

    /// <summary>Anima la escala de <paramref name="t"/> de 0 al valor original con un ligero rebote.</summary>
    /// <param name="t">Transform a animar; tolera destrucción a mitad de la animación.</param>
    /// <param name="time">Duración en segundos.</param>
    /// <returns>Enumerador de corrutina.</returns>
    static IEnumerator PopIn(Transform t, float time)
    {
        if (t == null) yield break;
        Vector3 target = t.localScale;
        t.localScale = Vector3.zero;
        float e = 0f;
        while (e < time && t != null)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time);
            float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
            t.localScale = target * (k * overshoot);
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

    /// <summary>
    /// Instancia el segmento de muro o puerta de un borde de celda, salvo que su estado sea <c>"CLEAR"</c>.
    /// </summary>
    /// <param name="row">Fila de la celda.</param>
    /// <param name="col">Columna de la celda.</param>
    /// <param name="side">Borde: <c>"Up"</c> | <c>"Down"</c> | <c>"Left"</c> | <c>"Right"</c>.</param>
    /// <param name="wallState">Estado del borde: <c>"WALL"</c>, <c>"DAMAGED_WALL"</c>, <c>"DOOR_OPEN"</c>, <c>"DOOR_CLOSE"</c>...</param>
    /// <param name="center">Centro de mundo de la celda (ver <see cref="CellCenter(int, int)"/>).</param>
    /// <remarks>
    /// El objeto se desplaza media celda hacia el borde indicado y se sitúa a <c>y = 0.75</c>.
    /// Los bordes izquierdo/derecho rotan el prefab 90°. Los muros (no puertas) se escalan a
    /// <c>(cellSize, 1.5, 0.15)</c>. Cada borde compartido se instancia dos veces, una por
    /// cada celda adyacente.
    /// </remarks>
    void PlaceEdge(int row, int col, string side, string wallState, Vector3 center)
    {
        if (wallState == "CLEAR") return;

        Vector3 pos = center;
        bool vertical = side == "Left" || side == "Right";

        switch (side)
        {
            case "Up":    pos += new Vector3(0, 0, cellSize / 2f); break;
            case "Down":  pos += new Vector3(0, 0, -cellSize / 2f); break;
            case "Left":  pos += new Vector3(-cellSize / 2f, 0, 0); break;
            case "Right": pos += new Vector3(cellSize / 2f, 0, 0); break;
        }
        pos.y = 0.75f;

        bool isDoor = wallState == "DOOR_OPEN" || wallState == "DOOR_CLOSE";
        GameObject prefab = wallPrefab;
        Transform parent = wallsParent;
        if (isDoor)
        {
            prefab = wallState == "DOOR_OPEN" ? doorOpenPrefab : doorClosedPrefab;
            parent = doorsParent;
        }

        Quaternion rot = vertical ? Quaternion.Euler(0, 90f, 0) : Quaternion.identity;
        GameObject go = Instantiate(prefab, pos, rot, parent);
        if (!isDoor)
            go.transform.localScale = new Vector3(cellSize, 1.5f, 0.15f);
    }

    /// <summary>Destruye todos los hijos de <paramref name="parent"/>.</summary>
    /// <param name="parent">Contenedor a vaciar.</param>
    void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
