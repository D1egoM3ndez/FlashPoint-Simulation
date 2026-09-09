// BoardBuilder.cs
// Instancia el tablero en la escena a partir de un BoardData (JSON del server).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardBuilder : MonoBehaviour
{
    public float cellSize = 2f;

    [Header("Animación")]
    public float hazardPopTime = 0.25f;   // duración del "pop" al aparecer un ghost/zombie

    // Agentes vivos indexados por id, para poder animarlos entre turnos.
    readonly Dictionary<int, GameObject> agentById = new Dictionary<int, GameObject>();

    [Header("Prefabs")]
    public GameObject floorInsidePrefab;
    public GameObject floorOutsidePrefab;
    public GameObject wallPrefab;
    public GameObject doorOpenPrefab;
    public GameObject doorClosedPrefab;
    public GameObject zombiePrefab;       // amenaza que se propaga (antes era firePrefab)
    public GameObject ghostPrefab;        // obstaculo que se propaga (antes era smokePrefab)
    public GameObject poiUnknownPrefab;
    public GameObject poiVictimPrefab;
    public GameObject poiFalsePrefab;
    public GameObject agentPrefab;        // cazafantasmas (1 solo tipo de agente)
    public GameObject sparkPrefab;        // chispas al spawn de zombie/ghost

    [Header("Contenedores (los del Paso 2)")]
    public Transform floorParent;
    public Transform wallsParent;
    public Transform doorsParent;
    public Transform fireParent;
    public Transform smokeParent;
    public Transform poisParent;
    public Transform agentsParent;

    bool cameraFitted = false;

    void Start()
    {
        // PlaybackController controla la carga del JSON.
    }

    public void FitCamera(int width, int height)
    {
        if (cameraFitted) return;
        cameraFitted = true;
        TopDownCamera cam = Camera.main.GetComponent<TopDownCamera>();
        if (cam != null)
            cam.FitToBoard(width, height, cellSize);
    }

    // ---- posiciones -------------------------------------------------------

    Vector3 CellCenter(int row, int col)
    {
        return new Vector3(col * cellSize, 0f, -row * cellSize);
    }

    bool IsOutside(int row, int col, int height, int width)
    {
        return row == 0 || row == height - 1 || col == 0 || col == width - 1;
    }

    // ---- dibujar ------------------------------------------------------------

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
            // Down/Right tambien se dibujan para no dejar huecos en el
            // borde final de la grilla (fila/columna mas alta)
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

    // ---- API de animación (usada por PlaybackController) -----------------

    public Vector3 CellWorld(int row, int col)
    {
        return CellCenter(row, col) + Vector3.up * 0.5f;
    }

    public GameObject GetAgentObject(int id)
    {
        return agentById.TryGetValue(id, out GameObject go) ? go : null;
    }

    public void ShowAgentIndicator(int agentId)
    {
        HideAllIndicators();
        if (!agentById.TryGetValue(agentId, out GameObject go)) return;
        Transform indicator = go.transform.Find("Turno");
        if (indicator != null) indicator.gameObject.SetActive(true);
    }

    public void HideAllIndicators()
    {
        foreach (var go in agentById.Values)
        {
            Transform indicator = go.transform.Find("Turno");
            if (indicator != null) indicator.gameObject.SetActive(false);
        }
    }

    // Instancia un ghost (SMOKE) o zombie (FIRE) en la celda con un "pop".
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
            float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);   // rebote suave
            t.localScale = target * (k * overshoot);
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

    void PlaceEdge(int row, int col, string side, string wallState, Vector3 center)
    {
        if (wallState == "CLEAR") return; // nada que dibujar

        Vector3 pos = center;
        bool vertical = side == "Left" || side == "Right"; // pared corre en Z, no en X

        switch (side)
        {
            case "Up":    pos += new Vector3(0, 0, cellSize / 2f); break;
            case "Down":  pos += new Vector3(0, 0, -cellSize / 2f); break;
            case "Left":  pos += new Vector3(-cellSize / 2f, 0, 0); break;
            case "Right": pos += new Vector3(cellSize / 2f, 0, 0); break;
        }
        pos.y = 0.75f; // mitad de la altura de la pared

        bool isDoor = wallState == "DOOR_OPEN" || wallState == "DOOR_CLOSE";
        GameObject prefab = wallPrefab;
        Transform parent = wallsParent;
        if (isDoor)
        {
            prefab = wallState == "DOOR_OPEN" ? doorOpenPrefab : doorClosedPrefab;
            parent = doorsParent;
        }

        // Tanto la reja (pared) como la tumba (puerta) son modelos con su propia proporcion,
        // no placas planas simetricas: intercambiar los ejes X/Z de la escala para las paredes
        // verticales las deformaba (estiraba postes/tumba en vez de solo alargar el tramo).
        // Se mantiene siempre la misma escala "natural" y se rota 90 grados para Left/Right.
        Quaternion rot = vertical ? Quaternion.Euler(0, 90f, 0) : Quaternion.identity;
        GameObject go = Instantiate(prefab, pos, rot, parent);
        if (!isDoor)
            go.transform.localScale = new Vector3(cellSize, 1.5f, 0.15f); // la reja si necesita alargarse al tamano de celda
    }

    void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
