// BoardBuilder.cs
// Instancia el tablero en la escena a partir de un BoardData.
// Por ahora BoardData sale de BuildTestBoard() (hardcodeado, copiado
// exactamente de fire/board.py -> create_board()). El dia que conectes
// el servidor, solo cambias de donde sale el BoardData - Render() no
// se toca.
using System.Collections.Generic;
using UnityEngine;

public class BoardBuilder : MonoBehaviour
{
    public float cellSize = 2f;

    [Header("Prefabs")]
    public GameObject floorInsidePrefab;
    public GameObject floorOutsidePrefab;
    public GameObject wallPrefab;
    public GameObject doorOpenPrefab;
    public GameObject doorClosedPrefab;
    public GameObject firePrefab;
    public GameObject smokePrefab;
    public GameObject poiUnknownPrefab;
    public GameObject poiVictimPrefab;
    public GameObject poiFalsePrefab;
    public GameObject extinguisherPrefab;
    public GameObject rescuerPrefab;

    [Header("Contenedores (los del Paso 2)")]
    public Transform floorParent;
    public Transform wallsParent;
    public Transform doorsParent;
    public Transform fireParent;
    public Transform smokeParent;
    public Transform poisParent;
    public Transform agentsParent;

    void Start()
    {
        BoardData data = BuildTestBoard();
        Render(data);

        TopDownCamera cam = Camera.main.GetComponent<TopDownCamera>();
        if (cam != null)
            cam.FitToBoard(data.width, data.height, cellSize);
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
                Instantiate(firePrefab, center + Vector3.up * 0.3f, Quaternion.identity, fireParent);
            else if (cell.state == "SMOKE")
                Instantiate(smokePrefab, center + Vector3.up * 0.3f, Quaternion.identity, smokeParent);
        }

        foreach (PoiData poi in data.pois)
        {
            Vector3 center = CellCenter(poi.row, poi.col) + Vector3.up * 0.5f;
            GameObject prefab = poiUnknownPrefab;
            if (poi.revealed)
                prefab = poi.kind == "VICTIM" ? poiVictimPrefab : poiFalsePrefab;
            Instantiate(prefab, center, Quaternion.identity, poisParent);
        }

        foreach (AgentData agent in data.agents)
        {
            Vector3 center = CellCenter(agent.row, agent.col) + Vector3.up * 0.5f;
            GameObject prefab = agent.role == "EXTINGUISHER" ? extinguisherPrefab : rescuerPrefab;
            Instantiate(prefab, center, Quaternion.identity, agentsParent);
        }
    }

    void PlaceEdge(int row, int col, string side, string wallState, Vector3 center)
    {
        if (wallState == "CLEAR") return; // nada que dibujar

        Vector3 pos = center;
        Vector3 scale = new Vector3(cellSize, 1.5f, 0.15f); // pared horizontal (Up/Down) por defecto

        switch (side)
        {
            case "Up":    pos += new Vector3(0, 0, cellSize / 2f); break;
            case "Down":  pos += new Vector3(0, 0, -cellSize / 2f); break;
            case "Left":  pos += new Vector3(-cellSize / 2f, 0, 0); scale = new Vector3(0.15f, 1.5f, cellSize); break;
            case "Right": pos += new Vector3(cellSize / 2f, 0, 0); scale = new Vector3(0.15f, 1.5f, cellSize); break;
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

        GameObject go = Instantiate(prefab, pos, Quaternion.identity, parent);
        go.transform.localScale = scale;
    }

    void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ---- datos de prueba (copia exacta de fire/board.py -> create_board) --

    BoardData BuildTestBoard()
    {
        int height = 8, width = 10;
        var cells = new Dictionary<(int, int), CellData>();
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                cells[(r, c)] = new CellData { row = r, col = c, state = "CLEAR", wallUp = "CLEAR", wallDown = "CLEAR", wallLeft = "CLEAR", wallRight = "CLEAR" };

        void SetWall(int r, int c, string dir, string state)
        {
            (int nr, int nc, string opp) = dir switch
            {
                "Up" => (r - 1, c, "Down"),
                "Down" => (r + 1, c, "Up"),
                "Left" => (r, c - 1, "Right"),
                "Right" => (r, c + 1, "Left"),
                _ => (r, c, dir),
            };
            SetField(cells[(r, c)], dir, state);
            if (cells.ContainsKey((nr, nc)))
                SetField(cells[(nr, nc)], opp, state);
        }

        // sellar el perimetro (borde del tablero completo + borde del edificio)
        for (int r = 0; r < height; r++) { SetWall(r, 0, "Left", "WALL"); SetWall(r, width - 1, "Right", "WALL"); }
        for (int c = 0; c < width; c++) { SetWall(0, c, "Up", "WALL"); SetWall(height - 1, c, "Down", "WALL"); }
        for (int r = 1; r <= 6; r++)
            for (int c = 1; c <= 8; c++)
            {
                if (r == 1) SetWall(r, c, "Up", "WALL");
                if (r == 6) SetWall(r, c, "Down", "WALL");
                if (c == 1) SetWall(r, c, "Left", "WALL");
                if (c == 8) SetWall(r, c, "Right", "WALL");
            }

        // paredes interiores (idem interior_walls en board.py)
        (int, int, string)[] interiorWalls = {
            (2,3,"Right"), (1,5,"Right"), (4,2,"Right"), (3,6,"Right"), (5,5,"Right"),
            (4,6,"Right"), (4,6, "Down"),  // Cocina / Cuarto de ninos, fila 4
            (2,3,"Down"), (2,4,"Down"), (2,5,"Down"), (2,6,"Down"), (2,7,"Down"),
            (4,1,"Down"), (4,2,"Down"), (4,3,"Down"), (4,5,"Down"), (4,8,"Down"), (4,7,"Down"), (5,7,"Right"),(6,5,"Down"),
        };
        foreach (var (r, c, dir) in interiorWalls) SetWall(r, c, dir, "WALL");

        // puertas (idem doors en board.py)
        (int, int, string, string)[] doors = {
            (1,4,"Left","DOOR_CLOSE"), (2,5,"Right","DOOR_CLOSE"), (3,2,"Right","DOOR_CLOSE"),
            (2,8,"Down","DOOR_CLOSE"), (4,4,"Down","DOOR_CLOSE"), (6,7,"Right", "DOOR_CLOSE"),
            (6,5,"Right","DOOR_CLOSE"),
            (1,6,"Up","DOOR_OPEN"), (3,1,"Left","DOOR_OPEN"), (4,8,"Right","DOOR_OPEN"),
            (6,3,"Down","DOOR_OPEN"), (4,7,"Left", "DOOR_CLOSE")
        };
        foreach (var (r, c, dir, state) in doors) SetWall(r, c, dir, state);

        // fuego inicial (idem fires en board.py)
        (int, int)[] fires = { (2,2), (2,3), (3,2), (3,3), (3,4), (3,5), (4,4), (5,6), (5,7), (6,6) };
        foreach (var (r, c) in fires) cells[(r, c)].state = "FIRE";

        var data = new BoardData { height = height, width = width, turn = 0, status = "in_progress" };
        data.cells.AddRange(cells.Values);

        (int, int)[] poiPositions = { (2, 4), (5, 1), (5, 8) };
        foreach (var (r, c) in poiPositions)
            data.pois.Add(new PoiData { row = r, col = c, revealed = false });

        // bomberos de ejemplo (para probar los prefabs de agentes)
        data.agents.Add(new AgentData { id = 1, row = 0, col = 0, role = "EXTINGUISHER" });
        data.agents.Add(new AgentData { id = 2, row = 0, col = 1, role = "EXTINGUISHER" });
        data.agents.Add(new AgentData { id = 3, row = 0, col = 2, role = "EXTINGUISHER" });
        data.agents.Add(new AgentData { id = 4, row = 0, col = 3, role = "RESCUER" });
        data.agents.Add(new AgentData { id = 5, row = 0, col = 4, role = "RESCUER" });
        data.agents.Add(new AgentData { id = 6, row = 0, col = 5, role = "RESCUER" });

        return data;
    }

    void SetField(CellData cell, string dir, string state)
    {
        switch (dir)
        {
            case "Up": cell.wallUp = state; break;
            case "Down": cell.wallDown = state; break;
            case "Left": cell.wallLeft = state; break;
            case "Right": cell.wallRight = state; break;
        }
    }
}
