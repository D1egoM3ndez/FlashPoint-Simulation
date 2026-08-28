from dataclasses import dataclass, field
from typing import Callable

import numpy as np

from .board import Board, CellState, PoiState, WallState, Direction


@dataclass
class FireEvent:
    """Todo lo que pasó durante una tirada de Avanzar Fuego, para que
    quien la llamó (el modelo) pueda reaccionar: derribar bomberos,
    contar víctimas perdidas y llevar la cuenta del daño total."""
    damage_added: int = 0
    ignited: set[tuple[int, int]] = field(default_factory=set)
    lost_pois: list[tuple[tuple[int, int], PoiState]] = field(default_factory=list)


def roll_target(board: Board, rng: np.random.Generator) -> tuple[int, int]:
    """Tira los dados para elegir una celda objetivo. Las coordenadas de
    dado solo están impresas en las celdas interiores del edificio, así
    que el objetivo siempre excluye el anillo de 1 celda de espacios de
    afuera que rodea al tablero."""
    row = int(rng.integers(1, board.height - 1))
    col = int(rng.integers(1, board.width - 1))
    return (row, col)


def _ignite(board: Board, pos: tuple[int, int], event: FireEvent):
    """Convierte una celda en Fuego y resuelve la pérdida de
    Víctima/POI que eso provoca (si había alguno ahí)."""
    if board.get_state(pos) == CellState.FIRE:
        return
    board.set_state(pos, CellState.FIRE)
    poi = board.poi_at(pos)
    if poi is not None:
        board.remove_poi(pos)
        event.lost_pois.append((pos, poi.kind))
    event.ignited.add(pos)


def advance_fire(
    board: Board,
    rng: np.random.Generator,
    target: tuple[int, int] | None = None,
    on_damage: Callable[[], bool] | None = None,
) -> FireEvent:
    """La fase 'Avanzar Fuego' de un turno: tira los dados (o usa
    `target` si se le da uno, para pruebas) y resuelve lo que corresponda
    según lo que había en esa celda: vacía -> Humo, Humo -> Fuego, Fuego
    -> Explosión. Al final siempre revisa si hay Flashover (humo pegado
    a fuego que también se prende).

    `on_damage`, si se le pasa, se llama justo después de colocar CADA
    marcador de daño individual (puede haber hasta 4 en una sola
    Explosión, uno por dirección). Debe aplicar ese punto de daño al
    total acumulado y devolver si la partida sigue en curso - devolver
    False detiene la Explosión para que no siga dañando más paredes en
    esta misma llamada, así el total nunca se pasa del umbral de
    colapso."""
    event = FireEvent()
    pos = target if target is not None else roll_target(board, rng)
    state = board.get_state(pos)

    match state:
        case CellState.CLEAR:
            board.set_state(pos, CellState.SMOKE)
        case CellState.SMOKE:
            _ignite(board, pos, event)
        case CellState.FIRE:
            explosion(board, pos, event, on_damage)
        case _:
            pass

    flashover(board, pos, event)
    return event


def explosion(board: Board, pos: tuple[int, int], event: FireEvent, on_damage: Callable[[], bool] | None = None):
    """Resuelve una Explosión: lanza una onda de choque en cada una de
    las 4 direcciones desde `pos`. Si `on_damage` avisa que la partida ya
    terminó (por ejemplo, el edificio acaba de colapsar), no sigue
    resolviendo las direcciones que faltaban."""
    for direction in Direction:
        keep_going = resolve_shockwave(board, pos, direction, event, on_damage)
        if not keep_going:
            return


def resolve_shockwave(
    board: Board,
    pos: tuple[int, int],
    direction: Direction,
    event: FireEvent,
    on_damage: Callable[[], bool] | None = None,
) -> bool:
    """Hace avanzar la onda de choque de una Explosión en una sola
    dirección hasta toparse con algo: una pared (le pone/aumenta daño),
    una puerta cerrada (la destruye), o una celda abierta/con humo (le
    prende fuego). Si sigue encontrando celdas en llamas, la onda
    continúa viajando a través de ellas.

    Devuelve si quien la llamó debe seguir resolviendo las demás
    direcciones (False solo cuando `on_damage` avisa que la partida ya
    terminó)."""
    current = pos

    while True:
        wall = board.get_wall(current, direction)
        if wall is None:
            return True

        if wall == WallState.WALL:
            board.set_wall(current, direction, WallState.DAMAGED_WALL)
            event.damage_added += 1
            return on_damage() if on_damage is not None else True
        if wall == WallState.DAMAGED_WALL:
            board.set_wall(current, direction, WallState.CLEAR)
            event.damage_added += 1
            return on_damage() if on_damage is not None else True
        if wall == WallState.DOOR_CLOSE:
            board.set_wall(current, direction, WallState.CLEAR)
            return True
        if wall == WallState.DOOR_OPEN:
            # Una puerta abierta golpeada por la onda de choque se
            # destruye, pero la onda sigue viajando a través de ella.
            board.set_wall(current, direction, WallState.CLEAR)

        nxt = board.neighbor(current, direction)
        if nxt is None:
            return True

        state = board.get_state(nxt)
        if state == CellState.FIRE:
            current = nxt
            continue
        if state == CellState.SMOKE:
            _ignite(board, nxt, event)
            return True
        _ignite(board, nxt, event)
        return True


def flashover(board: Board, pos: tuple[int, int], event: FireEvent):
    """Propaga el fuego por contagio: cualquier celda con Humo que esté
    pegada (adyacente, sin pared/puerta cerrada de por medio) a una
    celda en Fuego también se prende, y así en cadena, hasta que ya no
    quede humo pegado a fuego en toda la zona conectada."""
    stack = [pos]
    visited = {pos}
    while stack:
        current = stack.pop()
        state = board.get_state(current)
        if state not in (CellState.SMOKE, CellState.FIRE):
            continue
        if state == CellState.SMOKE:
            _ignite(board, current, event)

        for direction in Direction:
            if not board.is_passable(current, direction):
                continue
            nxt = board.neighbor(current, direction)
            if nxt is not None and nxt not in visited:
                visited.add(nxt)
                stack.append(nxt)


def replenish_poi(
    board: Board,
    rng: np.random.Generator,
    draw_kind: Callable[[], PoiState | None],
    firefighter_positions: set[tuple[int, int]],
    target_count: int = 3,
) -> list[tuple[int, int]]:
    """Fase 'Reponer POI': tira los dados y coloca marcadores nuevos
    hasta que en el tablero haya `target_count` POI sin identificar más
    Víctimas ya reveladas, o hasta que se acabe el pool de marcadores.
    `draw_kind` saca (y consume) un marcador del pool restante de
    Víctimas/Falsas alarmas - ver `FirefighterModel._draw_poi_kind`.
    Si el dado cae sobre un bombero, el POI se revela de inmediato,
    igual que en las reglas."""
    placed = []
    while len(board.pois) < target_count:
        pos = roll_target(board, rng)
        if board.has_poi(pos):
            continue  # se vuelve a tirar: ya hay un POI ahí

        if board.get_state(pos) in (CellState.FIRE, CellState.SMOKE):
            board.set_state(pos, CellState.CLEAR)

        kind = draw_kind()
        if kind is None:
            break  # ya no quedan marcadores en la caja

        board.add_poi(pos, kind)
        placed.append(pos)
        if pos in firefighter_positions:
            board.reveal_poi(pos)

    return placed
