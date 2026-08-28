// BoardData.cs
// Estructuras de datos que representan un snapshot del tablero de Python.
// Coinciden con lo que devolvera el servidor mas adelante (fire/model.py).
using System;
using System.Collections.Generic;

[Serializable]
public class CellData
{
    public int row;
    public int col;
    public string state;       // "CLEAR" | "SMOKE" | "FIRE"
    public string wallUp;      // "CLEAR" | "WALL" | "DAMAGED_WALL" | "DOOR_OPEN" | "DOOR_CLOSE"
    public string wallDown;
    public string wallLeft;
    public string wallRight;
}

[Serializable]
public class AgentData
{
    public int id;
    public int row;
    public int col;
    public string role;        // "EXTINGUISHER" | "RESCUER"
    public int ap;
    public bool carrying;
    public bool knockedDown;
}

[Serializable]
public class PoiData
{
    public int row;
    public int col;
    public bool revealed;
    public string kind;        // "VICTIM" | "FALSE" (solo tiene sentido si revealed == true)
}

[Serializable]
public class BoardData
{
    public int height;         // filas totales (8)
    public int width;          // columnas totales (10)
    public int turn;
    public string status;      // "in_progress" | "won" | "lost"
    public int damageTotal;
    public int victimsRescued;
    public int victimsLost;

    public List<CellData> cells = new List<CellData>();
    public List<AgentData> agents = new List<AgentData>();
    public List<PoiData> pois = new List<PoiData>();
}
