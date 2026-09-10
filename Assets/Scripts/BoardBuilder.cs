// BoardBuilder.cs
// Instancia el tablero en la escena a partir de un BoardData (JSON del server).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardBuilder : MonoBehaviour
{
    public float cellSize = 2f;

    [Header("Animación")]
    public float hazardPopTime = 0.25f;

    readonly Dictionary<int, GameObject> agentById = new Dictionary<int, GameObject>();

    [Header("Prefabs")]
    public GameObject floorInsidePrefab;
    public GameObject floorOutsidePrefab;
    public GameObject wallPrefab;
    public GameObject doorOpenPrefab;
    public GameObject doorClosedPrefab;
    public GameObject zombiePrefab;
    public GameObject ghostPrefab;
    public GameObject poiUnknownPrefab;
    public GameObject poiVictimPrefab;
    public GameObject poiFalsePrefab;
    public GameObject agentPrefab;
    public GameObject sparkPrefab;

    [Header("Contenedores")]
    public Transform floorParent;
    public Transform wallsParent;
    public Transform doorsParent;
    public Transform fireParent;
    public Transform smokeParent;
    public Transform poisParent;
    public Transform agentsParent;

    bool cameraFitted = false;

    public void FitCamera(int width, int height)
    {
        if (cameraFitted) return;
        cameraFitted = true;
        TopDownCamera cam = Camera.main.GetComponent<TopDownCamera>();
        if (cam != null)
            cam.FitToBoard(width, height, cellSize);
    }

    Vector3 CellCenter(int row, int col)
    {
        return new Vector3(col * cellSize, 0f, -row * cellSize);
    }

    bool IsOutside(int row, int col, int height, int width)
    {
        return row == 0 || row == height - 1 || col == 0 || col == width - 1;
    }

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
            float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
            t.localScale = target * (k * overshoot);
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

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

    void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
