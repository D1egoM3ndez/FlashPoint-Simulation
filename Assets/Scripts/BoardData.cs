// BoardData.cs
// Formato interno (BoardBuilder) + parser JSON del partner (data_collector.py).
using System;
using System.Collections.Generic;
using UnityEngine;   // JsonUtility

// ── Formato interno (BoardBuilder lo consume) ─────────────────────────────

/// <summary>Estado de una celda del tablero y de sus cuatro bordes.</summary>
[Serializable]
public class CellData
{
    /// <summary>Fila de la celda (0 arriba).</summary>
    public int row;
    /// <summary>Columna de la celda (0 a la izquierda).</summary>
    public int col;
    /// <summary>Contenido de la celda: <c>"CLEAR"</c> | <c>"SMOKE"</c> | <c>"FIRE"</c>.</summary>
    public string state;
    /// <summary>Borde superior: <c>"CLEAR"</c> | <c>"WALL"</c> | <c>"DAMAGED_WALL"</c> | <c>"DOOR_OPEN"</c> | <c>"DOOR_CLOSE"</c>.</summary>
    public string wallUp;
    /// <summary>Borde inferior. Mismos valores que <see cref="wallUp"/>.</summary>
    public string wallDown;
    /// <summary>Borde izquierdo. Mismos valores que <see cref="wallUp"/>.</summary>
    public string wallLeft;
    /// <summary>Borde derecho. Mismos valores que <see cref="wallUp"/>.</summary>
    public string wallRight;
}

/// <summary>Estado de un bombero (agente) en un turno concreto.</summary>
[Serializable]
public class AgentData
{
    /// <summary>Identificador estable del agente entre turnos.</summary>
    public int id;
    public int row;
    public int col;
    /// <summary>Rol: <c>"rescue"</c> | <c>"fire_suppression"</c> | <c>"evacuation"</c>.</summary>
    public string role;
    /// <summary>Puntos de acción disponibles.</summary>
    public int ap;
    /// <summary>Puntos de acción guardados para el siguiente turno.</summary>
    public int savedAp;
    /// <summary><c>true</c> si transporta a una víctima.</summary>
    public bool carrying;
    /// <summary><c>true</c> si está derribado (knocked down).</summary>
    public bool knockedDown;
}

/// <summary>Punto de interés (POI) sobre el tablero: víctima o falsa alarma.</summary>
[Serializable]
public class PoiData
{
    public int row;
    public int col;
    /// <summary><c>true</c> si ya se reveló su naturaleza.</summary>
    public bool revealed;
    /// <summary>Tipo: <c>"VICTIM"</c> | <c>"FALSE"</c>.</summary>
    public string kind;
}

/// <summary>Un paso de desplazamiento del actor dentro de un turno.</summary>
[Serializable]
public class ActorStep
{
    /// <summary>Dirección del paso: <c>"UP"</c> | <c>"DOWN"</c> | <c>"LEFT"</c> | <c>"RIGHT"</c>.</summary>
    public string direction;   // "UP" | "DOWN" | "LEFT" | "RIGHT"
}

/// <summary>
/// Bombero que actuó en un turno y trayectoria que recorrió. Se usa para animar el playback.
/// </summary>
// Bombero que actuó en un turno: sirve para animar el playback.
[Serializable]
public class ActorData
{
    /// <summary>Id del agente que actuó (coincide con <see cref="AgentData.id"/>).</summary>
    public int id;
    /// <summary>Casilla de partida al inicio del turno.</summary>
    public int startRow, startCol;
    /// <summary>Casilla de destino tras aplicar todos los <see cref="steps"/>.</summary>
    public int finalRow, finalCol;
    /// <summary>Solo los pasos de tipo <c>"move"</c>, en orden de ejecución.</summary>
    public List<ActorStep> steps = new List<ActorStep>();  // solo los "move", en orden

    /// <summary><c>true</c> si hay al menos un paso que animar.</summary>
    public bool HasPath => steps != null && steps.Count > 0;
}

/// <summary>
/// Estado completo del tablero en un turno: el formato que consume <see cref="BoardBuilder"/>.
/// </summary>
[Serializable]
public class BoardData
{
    /// <summary>Alto del tablero en celdas.</summary>
    public int height;
    /// <summary>Ancho del tablero en celdas.</summary>
    public int width;
    /// <summary>Índice/número de turno (0 = estado inicial).</summary>
    public int turn;
    /// <summary>Estado de la partida: <c>"in_progress"</c> | <c>"won"</c> | <c>"lost"</c>.</summary>
    public string status;      // "in_progress" | "won" | "lost"
    public int damageTotal;
    public int victimsRescued;
    public int victimsLost;
    public int falseAlarmsFound;

    /// <summary>Todas las celdas del tablero, en orden fila-mayor (<c>row * width + col</c>).</summary>
    public List<CellData> cells = new List<CellData>();
    public List<AgentData> agents = new List<AgentData>();
    public List<PoiData> pois = new List<PoiData>();

    // ── Datos de animación ──

    /// <summary>Agente que se movió este turno; <c>null</c> en el turno 0 o si nadie actuó.</summary>
    public ActorData actor;                            // quién se movió este turno (null en el turno 0)
    /// <summary>
    /// Celdas que pasaron a FIRE/SMOKE este turno, aplanadas como <c>[r, c, r, c, ...]</c>.
    /// Calculado por diferencia con el turno anterior en <see cref="BoardDataConverter.ParseJsonArray(string)"/>.
    /// </summary>
    public List<int> ignitedFlat = new List<int>();    // celdas nuevas de FIRE/SMOKE este turno: [r,c,r,c,...]
}

// ── Formato JSON del partner ──────────────────────────────────────────────
// Nota: JsonUtility NO soporta string[][] ni int[][].
// Usamos List<string> aplanados y accedemos con [fila * cols + col].

/// <summary>
/// DTOs que reflejan literalmente el JSON generado por <c>data_collector.py</c>. Los nombres
/// en <c>snake_case</c> son obligatorios: <see cref="JsonUtility"/> empareja campos por nombre
/// exacto. Las matrices anidadas del JSON se rellenan aparte
/// (ver <see cref="BoardDataConverter.ParseNestedParts"/>) porque <see cref="JsonUtility"/> no
/// deserializa arrays de arrays.
/// </summary>
namespace JsonFormat
{
    /// <summary>Un snapshot del tablero: elemento del array JSON de nivel superior.</summary>
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

    /// <summary>Agente que actuó en el turno y la lista de acciones que ejecutó.</summary>
    [Serializable] public class JsonActor
    {
        public int id;
        /// <summary>Posición inicial <c>[row, col]</c>.</summary>
        public int[] start_pos;
        public List<JsonAction> actions;
        public int action_count;
        public int ap_spent;
    }

    /// <summary>Acción individual del actor. Los campos aplicables dependen de <see cref="action"/>.</summary>
    [Serializable] public class JsonAction
    {
        public string action;      // "move"|"door"|"chop"|"extinguish"|"reveal"|"rescue"
        public string direction;   // "UP"|"DOWN"|"LEFT"|"RIGHT"
        public string state;       // "SMOKE"|"FIRE" (extinguish)
        public int[] pos;          // [row,col] (extinguish)
        public string kind;        // "VICTIM"|"FALSE" (reveal)
    }

    /// <summary>Marcador acumulado de la partida en el snapshot.</summary>
    [Serializable] public class JsonScore
    {
        public int damage_total;
        public int victims_rescued;
        public int victims_lost;
        public int false_alarms_found;
    }

    /// <summary>Resumen de la propagación del fuego ocurrida en el turno.</summary>
    [Serializable] public class JsonFireEvent
    {
        public int ignited_count;
        /// <summary>Celdas encendidas, aplanadas: <c>[r1, c1, r2, c2, ...]</c>.</summary>
        public List<int> ignited_cells_flat;   // aplanado: [r1,c1,r2,c2,...]
        public int lost_poi_count;
        public List<JsonLostPoi> lost_pois;
        public int damage_added;
    }

    /// <summary>POI perdido por el avance del fuego.</summary>
    [Serializable] public class JsonLostPoi
    {
        public int[] pos;
        public string kind;
    }

    /// <summary>Contenido del tablero: estados de celda y muros, todo aplanado.</summary>
    [Serializable] public class JsonBoard
    {
        /// <summary>Estados de celda aplanados: 80 elementos (8x10), índice <c>row * 10 + col</c>.</summary>
        public List<string> cells_flat;        // aplanado: 80 elems (8x10)
        public JsonWalls walls;
    }

    /// <summary>
    /// Segmentos de muro entre celdas, aplanados por orientación. Convención del partner
    /// (inversa a la lectura ingenua): <see cref="horizontal_flat"/> son los bordes
    /// izquierdo/derecho y <see cref="vertical_flat"/> los bordes superior/inferior.
    /// </summary>
    [Serializable] public class JsonWalls
    {
        /// <summary>Muros entre celdas contiguas en horizontal (izq/der): 72 elementos (8x9).</summary>
        public List<string> horizontal_flat;   // aplanado: 72 elems (8x9)
        /// <summary>Muros entre celdas contiguas en vertical (arr/abj): 70 elementos (7x10).</summary>
        public List<string> vertical_flat;     // aplanado: 70 elems (7x10)
    }

    /// <summary>Estado de un bombero en el snapshot.</summary>
    [Serializable] public class JsonFirefighter
    {
        public int id;
        /// <summary>Posición <c>[row, col]</c>.</summary>
        public int[] pos;
        public string role;
        public int ap;
        public int saved_ap;
        public bool carrying;
        public bool knocked_down;
    }

    /// <summary>POI vivo presente en el snapshot.</summary>
    [Serializable] public class JsonPoi
    {
        public int[] pos;
        public string kind;
        public bool revealed;
    }
}

// ── Conversor JSON → Formato interno ──────────────────────────────────────

/// <summary>
/// Convierte el JSON de <c>data_collector.py</c> en la lista de <see cref="BoardData"/> que
/// consume el resto del juego.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonUtility"/> no deserializa arrays anidados, así que el proceso es mixto:
/// (1) <see cref="PrepareJsonForUtility"/> reescribe las matrices anidadas como arrays planos
/// para que <see cref="JsonUtility.FromJson{T}(string)"/> pueda con el resto del objeto;
/// (2) <see cref="ParseNestedParts"/> vuelve a extraer esas matrices con un parser textual
/// propio y rellena los campos <c>*_flat</c>.
/// </para>
/// <para>
/// El parser textual (<see cref="ReplaceNestedArray"/>, <see cref="ParseNestedStringArray"/>)
/// asume tokens sin comas ni corchetes internos y solo trata la primera aparición de cada
/// nombre de campo; es frágil ante formatos JSON inesperados.
/// </para>
/// </remarks>
public static class BoardDataConverter
{
    /// <summary>Dimensiones fijas del tablero en celdas (alto <c>H</c> x ancho <c>W</c>).</summary>
    const int H = 8, W = 10;
    /// <summary>Nº de columnas del array aplanado de muros horizontales (8 filas x 9).</summary>
    const int WALLS_H_COLS = 9;   // horizontal: 8 filas x 9 columnas
    /// <summary>Nº de columnas del array aplanado de muros verticales (7 filas x 10).</summary>
    const int WALLS_V_COLS = 10;  // vertical: 7 filas x 10 columnas

    // ── Helper: parsear JSON anidado sin usar string[][] ──
    // JsonUtility no soporta arrays anidados, así que parseamos
    // el JSON manualmente para las partes anidadas.

    /// <summary>
    /// Traduce un <see cref="JsonFormat.Snapshot"/> ya deserializado al formato interno
    /// <see cref="BoardData"/>: reconstruye las celdas con sus cuatro bordes, los agentes,
    /// los POIs y la trayectoria del actor.
    /// </summary>
    /// <param name="s">
    /// Snapshot de origen. Debe tener <c>score</c> y <c>board</c> (y <c>board.walls</c>) no nulos,
    /// y los arrays <c>*_flat</c> ya rellenos (ver <see cref="ParseNestedParts"/>).
    /// </param>
    /// <returns>El <see cref="BoardData"/> equivalente, con <c>height = H</c> y <c>width = W</c>.</returns>
    /// <exception cref="NullReferenceException">
    /// Si <paramref name="s"/>.<c>score</c>, <c>board</c> o <c>board.walls</c> son <c>null</c>,
    /// o si algún <c>pos</c>/<c>start_pos</c> esperado es <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Complejidad O(H·W + agentes + pois + pasos del actor). Los bordes del perímetro se
    /// fuerzan a <c>"WALL"</c>. Convención del partner: los bordes superior/inferior se leen de
    /// <c>walls.vertical_flat</c> y los laterales de <c>walls.horizontal_flat</c>.
    /// </remarks>
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

    /// <summary>Lectura segura de un elemento de una lista aplanada.</summary>
    /// <param name="flat">Lista aplanada (puede ser <c>null</c>).</param>
    /// <param name="idx">Índice a leer.</param>
    /// <param name="fallback">Valor devuelto si el índice está fuera de rango o el elemento es <c>null</c>.</param>
    /// <returns>El elemento en <paramref name="idx"/>, o <paramref name="fallback"/>.</returns>
    static string GetFlat(List<string> flat, int idx, string fallback)
    {
        if (flat == null || idx < 0 || idx >= flat.Count) return fallback;
        return flat[idx] ?? fallback;
    }

    // ── Parsear JSON manualmente para arrays anidados ──

    /// <summary>
    /// Extrae una matriz <c>string[rows][cols]</c> de un fragmento JSON localizando sub-arrays
    /// <c>[ ... ]</c> consecutivos y separando por comas.
    /// </summary>
    /// <param name="json">Fragmento JSON que comienza en (o antes de) la matriz buscada.</param>
    /// <param name="rows">Número de filas esperadas.</param>
    /// <param name="cols">Número de columnas esperadas por fila.</param>
    /// <returns>
    /// Matriz <paramref name="rows"/> x <paramref name="cols"/>; las posiciones no encontradas
    /// quedan en <c>null</c>. Se recortan comillas y espacios de cada token.
    /// </returns>
    /// <remarks>
    /// Asume que los valores no contienen <c>','</c>, <c>'['</c> ni <c>']'</c>.
    /// Complejidad O(longitud del fragmento).
    /// </remarks>
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

    /// <summary>Aplana una matriz <c>string[][]</c> a una lista en orden fila-mayor.</summary>
    /// <param name="nested">Matriz de origen; se toleran <c>null</c> en la matriz o en filas sueltas.</param>
    /// <returns>Lista con los elementos concatenados fila por fila.</returns>
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
    /// Punto de entrada del conversor: transforma el texto JSON con el array de snapshots en
    /// la lista de turnos <see cref="BoardData"/>.
    /// </summary>
    /// <param name="json">Respuesta cruda del servidor o contenido del archivo de respaldo.</param>
    /// <returns>
    /// Lista de turnos en orden. Vacía si <paramref name="json"/> no contiene objetos; los
    /// snapshots con formato inesperado se omiten silenciosamente.
    /// </returns>
    /// <remarks>
    /// <para>Pasos: (1) trocea el array de nivel superior contando llaves <c>{}</c> —evita pasar
    /// un array en la raíz a <see cref="JsonUtility"/>—; (2) por cada objeto: pre-procesa,
    /// deserializa, rellena las matrices anidadas y convierte con <see cref="FromSnapshot"/>;
    /// (3) calcula <see cref="BoardData.ignitedFlat"/> comparando cada turno con el anterior.</para>
    /// <para>Complejidad O(N + Σ|snapshot_i| + T·H·W), con N = longitud del JSON y T = nº de turnos.</para>
    /// </remarks>
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

    /// <summary>
    /// Reescribe en <paramref name="json"/> las matrices anidadas de <c>cells</c>,
    /// <c>horizontal</c> y <c>vertical</c> como arrays planos, para que
    /// <see cref="JsonUtility.FromJson{T}(string)"/> pueda deserializar el resto del objeto.
    /// </summary>
    /// <param name="json">JSON de un único snapshot.</param>
    /// <returns>El JSON con esas tres matrices aplanadas.</returns>
    static string PrepareJsonForUtility(string json)
    {
        // Reemplazar arrays anidados de strings por arrays planos
        // JsonUtility no maneja string[][].
        string result = ReplaceNestedArray(json, "cells");
        result = ReplaceNestedArray(result, "horizontal");
        result = ReplaceNestedArray(result, "vertical");
        return result;
    }

    /// <summary>
    /// Sustituye la primera aparición del array externo asociado a <paramref name="fieldName"/>
    /// por un array plano con todos los elementos de sus sub-arrays.
    /// </summary>
    /// <param name="json">JSON de origen.</param>
    /// <param name="fieldName">Nombre del campo cuyo valor es una matriz anidada.</param>
    /// <returns>
    /// El JSON con el array aplanado, o el original sin cambios si no se localiza el campo o
    /// la estructura <c>[</c>...<c>]</c> esperada.
    /// </returns>
    /// <remarks>Solo trata la primera coincidencia de <paramref name="fieldName"/>. Complejidad O(longitud del JSON).</remarks>
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

    /// <summary>
    /// Rellena <c>board.cells_flat</c>, <c>walls.horizontal_flat</c> y <c>walls.vertical_flat</c>
    /// del <paramref name="snap"/> parseando las matrices anidadas del JSON original.
    /// </summary>
    /// <param name="rawJson">JSON original del snapshot (antes de <see cref="PrepareJsonForUtility"/>).</param>
    /// <param name="snap">Snapshot a completar; se modifican <c>board</c> y <c>board.walls</c> in situ.</param>
    /// <remarks>
    /// Dimensiones asumidas: <c>cells</c> 8x10, <c>walls.horizontal</c> 8x9, <c>walls.vertical</c> 7x10.
    /// No hace nada para una sección si <c>snap.board</c> o <c>snap.board.walls</c> son <c>null</c>.
    /// </remarks>
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

    /// <summary>Contenedor auxiliar para deserializar un array de snapshots. Actualmente sin uso.</summary>
    [Serializable] private class JsonArrayWrapper
    {
        public JsonFormat.Snapshot[] snapshots;
    }
}
