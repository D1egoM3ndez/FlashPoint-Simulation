// BoardData.cs
// Formato interno (BoardBuilder) + parser JSON del partner (data_collector.py).
using System;
using System.Collections.Generic;
using UnityEngine;   // JsonUtility

// ── Formato interno (BoardBuilder lo consume) ─────────────────────────────

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
    public string role;        // "rescue" | "fire_suppression" | "evacuation"
    public int ap;
    public int savedAp;
    public bool carrying;
    public bool knockedDown;
}

[Serializable]
public class PoiData
{
    public int row;
    public int col;
    public bool revealed;
    public string kind;        // "VICTIM" | "FALSE"
}

[Serializable]
public class ActorStep
{
    public string direction;   // "UP" | "DOWN" | "LEFT" | "RIGHT"
}

// Bombero que actuó en un turno: sirve para animar el playback.
[Serializable]
public class ActorData
{
    public int id;
    public int startRow, startCol;
    public int finalRow, finalCol;
    public List<ActorStep> steps = new List<ActorStep>();  // solo los "move", en orden

    public bool HasPath => steps != null && steps.Count > 0;
}

[Serializable]
public class BoardData
{
    public int height;
    public int width;
    public int turn;
    public string status;      // "in_progress" | "won" | "lost"
    public int damageTotal;
    public int victimsRescued;
    public int victimsLost;
    public int falseAlarmsFound;

    public List<CellData> cells = new List<CellData>();
    public List<AgentData> agents = new List<AgentData>();
    public List<PoiData> pois = new List<PoiData>();

    // ── Datos de animación ──
    public ActorData actor;                            // quién se movió este turno (null en el turno 0)
    public List<int> ignitedFlat = new List<int>();    // celdas nuevas de FIRE/SMOKE este turno: [r,c,r,c,...]
}

// ── Formato JSON del partner ──────────────────────────────────────────────
// Nota: JsonUtility NO soporta string[][] ni int[][].
// Usamos List<string> aplanados y accedemos con [fila * cols + col].

namespace JsonFormat
{
    [Serializable] public class Snapshot
    {
        public int turn;
        public string status;
        public JsonActor actor;
        public JsonScore score;
        public JsonFireEvent fire_event;
        public JsonBoard board;
        public List<JsonFirefighter> firefighters;
        public List<JsonPoi> pois;
    }

    [Serializable] public class JsonActor
    {
        public int id;
        public int[] start_pos;
        public List<JsonAction> actions;
        public int action_count;
        public int ap_spent;
    }

    [Serializable] public class JsonAction
    {
        public string action;      // "move"|"door"|"chop"|"extinguish"|"reveal"|"rescue"
        public string direction;   // "UP"|"DOWN"|"LEFT"|"RIGHT"
        public string state;       // "SMOKE"|"FIRE" (extinguish)
        public int[] pos;          // [row,col] (extinguish)
        public string kind;        // "VICTIM"|"FALSE" (reveal)
    }

    [Serializable] public class JsonScore
    {
        public int damage_total;
        public int victims_rescued;
        public int victims_lost;
        public int false_alarms_found;
    }

    [Serializable] public class JsonFireEvent
    {
        public int ignited_count;
        public List<int> ignited_cells_flat;   // aplanado: [r1,c1,r2,c2,...]
        public int lost_poi_count;
        public List<JsonLostPoi> lost_pois;
        public int damage_added;
    }

    [Serializable] public class JsonLostPoi
    {
        public int[] pos;
        public string kind;
    }

    [Serializable] public class JsonBoard
    {
        public List<string> cells_flat;        // aplanado: 80 elems (8x10)
        public JsonWalls walls;
    }

    [Serializable] public class JsonWalls
    {
        public List<string> horizontal_flat;   // aplanado: 72 elems (8x9)
        public List<string> vertical_flat;     // aplanado: 70 elems (7x10)
    }

    [Serializable] public class JsonFirefighter
    {
        public int id;
        public int[] pos;
        public string role;
        public int ap;
        public int saved_ap;
        public bool carrying;
        public bool knocked_down;
    }

    [Serializable] public class JsonPoi
    {
        public int[] pos;
        public string kind;
        public bool revealed;
    }
}

// ── Conversor JSON → Formato interno ──────────────────────────────────────

public static class BoardDataConverter
{
    const int H = 8, W = 10;
    const int WALLS_H_COLS = 9;   // horizontal: 8 filas x 9 columnas
    const int WALLS_V_COLS = 10;  // vertical: 7 filas x 10 columnas

    // ── Helper: parsear JSON anidado sin usar string[][] ──
    // JsonUtility no soporta arrays anidados, así que parseamos
    // el JSON manualmente para las partes anidadas.

    public static BoardData FromSnapshot(JsonFormat.Snapshot s)
    {
        var d = new BoardData
        {
            height = H, width = W,
            turn = s.turn, status = s.status,
            damageTotal = s.score.damage_total,
            victimsRescued = s.score.victims_rescued,
            victimsLost = s.score.victims_lost,
            falseAlarmsFound = s.score.false_alarms_found
        };

        // Celdas: convertir de flat arrays a formato por-celda
        for (int r = 0; r < H; r++)
        {
            for (int c = 0; c < W; c++)
            {
                int cellIdx = r * W + c;
                int hIdx = r * WALLS_H_COLS + c;
                int vIdx = r * WALLS_V_COLS + c;

                string cellState = GetFlat(s.board.cells_flat, cellIdx, "CLEAR");
                string wallUp    = (r > 0)     ? GetFlat(s.board.walls.vertical_flat, (r - 1) * WALLS_V_COLS + c, "WALL") : "WALL";
                string wallDown  = (r < H - 1) ? GetFlat(s.board.walls.vertical_flat, r * WALLS_V_COLS + c, "WALL")         : "WALL";
                string wallLeft  = (c > 0)     ? GetFlat(s.board.walls.horizontal_flat, r * WALLS_H_COLS + (c - 1), "WALL") : "WALL";
                string wallRight = (c < W - 1) ? GetFlat(s.board.walls.horizontal_flat, r * WALLS_H_COLS + c, "WALL")        : "WALL";

                d.cells.Add(new CellData
                {
                    row = r, col = c,
                    state = cellState,
                    wallUp = wallUp, wallDown = wallDown,
                    wallLeft = wallLeft, wallRight = wallRight
                });
            }
        }

        // Agentes
        foreach (var ff in s.firefighters)
        {
            d.agents.Add(new AgentData
            {
                id = ff.id, row = ff.pos[0], col = ff.pos[1],
                role = ff.role, ap = ff.ap, savedAp = ff.saved_ap,
                carrying = ff.carrying, knockedDown = ff.knocked_down
            });
        }

        // POIs (solo los vivos)
        if (s.pois != null)
        {
            foreach (var p in s.pois)
            {
                d.pois.Add(new PoiData
                {
                    row = p.pos[0], col = p.pos[1],
                    kind = p.kind, revealed = p.revealed
                });
            }
        }

        // Actor: el bombero que se movió este turno (para animar el playback).
        if (s.actor != null && s.actor.start_pos != null && s.actor.start_pos.Length == 2
            && s.actor.actions != null && s.actor.action_count > 0)
        {
            var a = new ActorData
            {
                id = s.actor.id,
                startRow = s.actor.start_pos[0],
                startCol = s.actor.start_pos[1]
            };
            int rr = a.startRow, cc = a.startCol;
            foreach (var act in s.actor.actions)
            {
                if (act == null || act.action != "move") continue;
                switch (act.direction)
                {
                    case "UP": rr--; break;
                    case "DOWN": rr++; break;
                    case "LEFT": cc--; break;
                    case "RIGHT": cc++; break;
                    default: continue;
                }
                a.steps.Add(new ActorStep { direction = act.direction });
            }
            a.finalRow = rr;
            a.finalCol = cc;
            d.actor = a;
        }

        return d;
    }

    static string GetFlat(List<string> flat, int idx, string fallback)
    {
        if (flat == null || idx < 0 || idx >= flat.Count) return fallback;
        return flat[idx] ?? fallback;
    }

    // ── Parsear JSON manualmente para arrays anidados ──

    static string[][] ParseNestedStringArray(string json, int rows, int cols)
    {
        var result = new string[rows][];
        // Buscar cada sub-array
        int searchStart = 0;
        for (int r = 0; r < rows; r++)
        {
            result[r] = new string[cols];
            int bracketStart = json.IndexOf('[', searchStart);
            if (bracketStart == -1) break;
            int bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd == -1) break;
            string rowStr = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            string[] parts = rowStr.Split(',');
            for (int c = 0; c < cols && c < parts.Length; c++)
            {
                result[r][c] = parts[c].Trim().Trim('"');
            }
            searchStart = bracketEnd + 1;
        }
        return result;
    }

    static List<string> FlattenNested(string[][] nested)
    {
        var flat = new List<string>();
        if (nested == null) return flat;
        for (int r = 0; r < nested.Length; r++)
        {
            if (nested[r] == null) continue;
            for (int c = 0; c < nested[r].Length; c++)
                flat.Add(nested[r][c]);
        }
        return flat;
    }

    /// <summary>
    /// Parsea un string JSON que contiene un array de snapshots.
    /// Maneja arrays anidados parseando manualmente las partes que JsonUtility no soporta.
    /// </summary>
    public static List<BoardData> ParseJsonArray(string json)
    {
        var result = new List<BoardData>();

        // Encontrar cada objeto snapshot en el array
        int depth = 0;
        int objStart = -1;
        var snapshotJsons = new List<string>();

        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '{')
            {
                if (depth == 0) objStart = i;
                depth++;
            }
            else if (json[i] == '}')
            {
                depth--;
                if (depth == 0 && objStart >= 0)
                {
                    snapshotJsons.Add(json.Substring(objStart, i - objStart + 1));
                    objStart = -1;
                }
            }
        }

        foreach (string snapJson in snapshotJsons)
        {
            try
            {
                // Reemplazar arrays anidados de strings por placeholders planos
                string processed = PrepareJsonForUtility(snapJson);

                var snap = JsonUtility.FromJson<JsonFormat.Snapshot>(processed);
                if (snap == null) continue;

                // Parsear las partes anidadas manualmente
                ParseNestedParts(snapJson, snap);

                result.Add(FromSnapshot(snap));
            }
            catch (Exception)
            {
                // snapshot con formato inesperado: se ignora y se sigue con el resto
            }
        }

        // Celdas encendidas por turno: se derivan diffeando con el turno anterior
        // (más fiable que parsear el "ignited_cells" anidado del JSON).
        for (int i = 1; i < result.Count; i++)
        {
            BoardData prev = result[i - 1];
            BoardData cur = result[i];
            foreach (CellData cell in cur.cells)
            {
                if (cell.state != "FIRE" && cell.state != "SMOKE") continue;
                int idx = cell.row * cur.width + cell.col;
                string before = (idx >= 0 && idx < prev.cells.Count) ? prev.cells[idx].state : "CLEAR";
                if (before != "FIRE" && before != "SMOKE")
                {
                    cur.ignitedFlat.Add(cell.row);
                    cur.ignitedFlat.Add(cell.col);
                }
            }
        }

        return result;
    }

    static string PrepareJsonForUtility(string json)
    {
        // Reemplazar arrays anidados de strings por arrays planos
        // JsonUtility no maneja string[][].
        string result = ReplaceNestedArray(json, "cells");
        result = ReplaceNestedArray(result, "horizontal");
        result = ReplaceNestedArray(result, "vertical");
        return result;
    }

    static string ReplaceNestedArray(string json, string fieldName)
    {
        int fieldIdx = json.IndexOf("\"" + fieldName + "\"");
        if (fieldIdx == -1) return json;

        // Buscar el ':' después del nombre del campo
        int colonIdx = json.IndexOf(':', fieldIdx);
        if (colonIdx == -1) return json;

        // Buscar el '[' del array externo
        int arrStart = json.IndexOf('[', colonIdx);
        if (arrStart == -1) return json;

        // Encontrar el final del array externo
        int depth = 0;
        int arrEnd = arrStart;
        while (arrEnd < json.Length)
        {
            if (json[arrEnd] == '[') depth++;
            else if (json[arrEnd] == ']') depth--;
            if (depth == 0) break;
            arrEnd++;
        }

        // Extraer todos los elementos de los sub-arrays
        string inner = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
        var elements = new List<string>();
        int searchStart = 0;
        while (searchStart < inner.Length)
        {
            int subStart = inner.IndexOf('[', searchStart);
            if (subStart == -1) break;
            int subEnd = inner.IndexOf(']', subStart);
            if (subEnd == -1) break;
            string subContent = inner.Substring(subStart + 1, subEnd - subStart - 1);
            string[] parts = subContent.Split(',');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    elements.Add(trimmed);
            }
            searchStart = subEnd + 1;
        }

        // Reconstruir como array plano
        string flatArray = "[" + string.Join(",", elements) + "]";
        return json.Substring(0, arrStart) + flatArray + json.Substring(arrEnd + 1);
    }

    static void ParseNestedParts(string rawJson, JsonFormat.Snapshot snap)
    {
        // Parsear board.cells como nested y convertir a flat
        int cellsIdx = rawJson.IndexOf("\"cells\"");
        if (cellsIdx != -1 && snap.board != null)
        {
            string[][] nested = ParseNestedStringArray(rawJson.Substring(cellsIdx), 8, 10);
            snap.board.cells_flat = FlattenNested(nested);
        }

        // Parsear walls.horizontal
        int hIdx = rawJson.IndexOf("\"horizontal\"");
        if (hIdx != -1 && snap.board?.walls != null)
        {
            string[][] nested = ParseNestedStringArray(rawJson.Substring(hIdx), 8, 9);
            snap.board.walls.horizontal_flat = FlattenNested(nested);
        }

        // Parsear walls.vertical
        int vIdx = rawJson.IndexOf("\"vertical\"");
        if (vIdx != -1 && snap.board?.walls != null)
        {
            string[][] nested = ParseNestedStringArray(rawJson.Substring(vIdx), 7, 10);
            snap.board.walls.vertical_flat = FlattenNested(nested);
        }

    }

    [Serializable] private class JsonArrayWrapper
    {
        public JsonFormat.Snapshot[] snapshots;
    }
}
