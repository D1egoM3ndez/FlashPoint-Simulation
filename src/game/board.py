import networkx as nx
from dataclasses import dataclass
from enum import Enum, auto

BOARD_HEIGHT = 8
BOARD_WIDTH = 10


class CellState(Enum):
    """Qué hay ardiendo (o no) en una celda."""
    CLEAR     = auto()
    SMOKE     = auto()
    FIRE      = auto()

class WallState(Enum):
    """Qué hay en el borde entre dos celdas: nada, una puerta (abierta o
    cerrada), o una pared (intacta o ya dañada por un golpe)."""
    CLEAR        = auto()
    DOOR_OPEN    = auto()
    DOOR_CLOSE   = auto()
    WALL         = auto()
    DAMAGED_WALL = auto()

class PoiState(Enum):
    """La identidad real de un marcador POI: Víctima o Falsa alarma."""
    VICTIM = auto()
    FALSE  = auto()


@dataclass
class POI:
    """Un marcador POI en el tablero. `kind` es su identidad real,
    fijada desde que se coloca pero oculta a los jugadores hasta que
    `revealed` sea True (igual que el juego físico: el reverso del
    marcador ya viene impreso desde la preparación, y solo se voltea
    cuando un bombero camina sobre él)."""
    pos: tuple[int, int]
    kind: PoiState
    revealed: bool = False


class Direction(Enum):
    """Las 4 direcciones ortogonales (arriba/abajo/izquierda/derecha)
    usadas para moverse y para identificar el borde de una celda."""
    UP    = (-1, 0)
    DOWN  = (1, 0)
    LEFT  = (0, -1)
    RIGHT = (0, 1)

    @property
    def opposite(self):
        """La dirección contraria (UP <-> DOWN, LEFT <-> RIGHT)."""
        return {
            Direction.UP: Direction.DOWN,
            Direction.DOWN: Direction.UP,
            Direction.LEFT: Direction.RIGHT,
            Direction.RIGHT: Direction.LEFT,
        }[self]


class Board:
    """El tablero completo: una cuadrícula (grafo de networkx) donde
    cada celda sabe si tiene fuego/humo, y cada borde entre dos celdas
    sabe si hay pared o puerta. También lleva la lista de marcadores POI
    que hay actualmente sobre el tablero."""

    def __init__(self, height: int = BOARD_HEIGHT, width: int = BOARD_WIDTH, pois: list[POI] | None = None):
        """Crea un tablero vacío de `height` x `width` celdas: todas
        libres de fuego/humo y sin ninguna pared (todo abierto). Quien
        llama (normalmente `create_board`, más abajo en este mismo
        archivo) es quien coloca después las paredes, puertas, fuego
        inicial y POI."""
        self.height = height
        self.width = width
        self.pois: list[POI] = pois if pois is not None else []
        self.G = nx.grid_2d_graph(height, width)

        nx.set_node_attributes(
            self.G, {node: CellState.CLEAR for node in self.G.nodes}, "state"
        )
        nx.set_node_attributes(
            self.G, {node: True for node in self.G.nodes}, "is_empty"
        )
        nx.set_edge_attributes(
            self.G, {edge: WallState.CLEAR for edge in self.G.edges}, "wall"
        )

    # ---- celdas ----------------------------------------------------

    def in_bounds(self, pos: tuple[int, int]) -> bool:
        """¿La celda `pos` existe dentro del tablero?"""
        row, col = pos
        return 0 <= row < self.height and 0 <= col < self.width

    def get_state(self, pos: tuple[int, int]) -> CellState:
        """El estado (vacía/humo/fuego) de esa celda."""
        return self.G.nodes[pos]["state"]

    def set_state(self, pos: tuple[int, int], state: CellState):
        """Cambia el estado (vacía/humo/fuego) de esa celda."""
        self.G.nodes[pos]["state"] = state

    def is_empty(self, pos: tuple[int, int]) -> bool:
        """(Sin uso actualmente por la simulación: el reto permite más
        de un bombero por celda, así que nada marca esto como ocupado.)"""
        return self.G.nodes[pos]["is_empty"]

    def clear_cell(self, pos: tuple[int, int]):
        """(Ver nota de `is_empty` - no se usa por ahora.)"""
        self.G.nodes[pos]["is_empty"] = True

    def fill_cell(self, pos: tuple[int, int]):
        """(Ver nota de `is_empty` - no se usa por ahora.)"""
        self.G.nodes[pos]["is_empty"] = False

    def is_outside(self, pos: tuple[int, int]) -> bool:
        """¿`pos` es una celda del anillo exterior (afuera del
        edificio)? Son las celdas del borde del tablero completo."""
        row, col = pos
        return row == 0 or row == self.height - 1 \
            or col == 0 or col == self.width - 1

    # ---- paredes / vecinos -----------------------------------------

    def neighbor(self, pos: tuple[int, int], direction: Direction) -> tuple[int, int] | None:
        """La celda vecina a `pos` en esa dirección, o None si se sale
        del tablero."""
        row, col = pos
        drow, dcol = direction.value
        n = (row + drow, col + dcol)
        return n if self.in_bounds(n) else None

    def get_wall(self, pos: tuple[int, int], direction: Direction) -> WallState | None:
        """Qué hay en el borde de `pos` hacia esa dirección (pared,
        puerta, o nada), o None si no hay celda vecina de ese lado."""
        n = self.neighbor(pos, direction)
        if n is None:
            return None
        return self.G[pos][n]["wall"]

    def set_wall(self, pos: tuple[int, int], direction: Direction, state: WallState):
        """Cambia lo que hay en el borde de `pos` hacia esa dirección.
        No hace nada si no hay celda vecina de ese lado (borde del mapa)."""
        n = self.neighbor(pos, direction)
        if n is None:
            return
        self.G[pos][n]["wall"] = state

    def is_passable(self, pos: tuple[int, int], direction: Direction) -> bool:
        """¿Se puede caminar de `pos` hacia esa dirección ahora mismo?
        (sin pared ni puerta cerrada de por medio). No tiene en cuenta
        si hay otros agentes en la celda destino."""
        wall = self.get_wall(pos, direction)
        if wall is None:
            return False
        return wall in (WallState.CLEAR, WallState.DOOR_OPEN)

    def open_neighbors(self, pos: tuple[int, int]) -> list[tuple[int, int]]:
        """Las celdas vecinas de `pos` a las que se puede pasar y que
        además están libres (ver nota de `is_empty`: en la práctica
        siempre están "libres" porque esa marca no se usa)."""
        result = []
        for direction in Direction:
            if self.is_passable(pos, direction):
                n = self.neighbor(pos, direction)
                if n is not None and self.is_empty(n):
                    result.append(n)
        return result

    # ---- POI  --------------------------------------------------------

    def has_poi(self, pos: tuple[int, int]) -> bool:
        """¿Hay un marcador POI (revelado o no) en esa celda?"""
        return self.poi_at(pos) is not None

    def poi_at(self, pos: tuple[int, int]) -> POI | None:
        """El marcador POI que está en esa celda, o None si no hay."""
        for poi in self.pois:
            if poi.pos == pos:
                return poi
        return None

    def add_poi(self, pos: tuple[int, int], kind: PoiState) -> POI:
        """Coloca un marcador POI nuevo en `pos`, con su identidad
        `kind` ya fija pero oculta (sin revelar)."""
        poi = POI(pos, kind)
        self.pois.append(poi)
        return poi

    def remove_poi(self, pos: tuple[int, int]):
        """Quita el marcador POI que esté en esa celda (si hay)."""
        self.pois = [poi for poi in self.pois if poi.pos != pos]

    def reveal_poi(self, pos: tuple[int, int]):
        """Voltea el marcador POI de esa celda. Su identidad ya estaba
        fija desde que se colocó (ver `add_poi`) - revelarlo no
        introduce ningún azar nuevo. Una Falsa alarma revelada se quita
        de inmediato del tablero."""
        poi = self.poi_at(pos)
        if poi is None:
            raise ValueError(f"No hay marcador POI en {pos}")
        poi.revealed = True
        if poi.kind == PoiState.FALSE:
            self.remove_poi(pos)
        return poi.kind

    def move_victim(self, pos: tuple[int, int], target: tuple[int, int]):
        """Mueve el marcador POI de `pos` a `target` (se usa cuando un
        bombero carga a una víctima de una celda a la siguiente)."""
        poi = self.poi_at(pos)
        if poi is not None:
            poi.pos = target

    # ---- depuración ----------------------------------------------------

    def dump(self):
        """Imprime el estado de todas las celdas, por si se quiere
        revisar el tablero a mano desde la terminal."""
        for node, data in self.G.nodes(data=True):
            print(node, data["state"])


# El interior del edificio mide 6 filas x 8 columnas (filas/columnas
# completas 1..6 y 1..8), rodeado por el anillo de 1 celda de "afuera"
# en los bordes del tablero completo (fila/columna 0 y la última).
INTERIOR_ROWS = BOARD_HEIGHT - 2  # 6
INTERIOR_COLS = BOARD_WIDTH - 2   # 8


def _seal_building_perimeter(board: Board):
    """Pone pared en el borde real del tablero completo, y en todo el
    perímetro exterior del edificio (celda interior <-> anillo de
    afuera) - por defecto el edificio queda cerrado por todos lados;
    `create_board` abre las puertas explícitamente después."""
    for row in range(board.height):
        board.set_wall((row, 0), Direction.LEFT, WallState.WALL)
        board.set_wall((row, board.width - 1), Direction.RIGHT, WallState.WALL)
    for col in range(board.width):
        board.set_wall((0, col), Direction.UP, WallState.WALL)
        board.set_wall((board.height - 1, col), Direction.DOWN, WallState.WALL)

    for row in range(1, INTERIOR_ROWS + 1):
        for col in range(1, INTERIOR_COLS + 1):
            if row == 1:
                board.set_wall((row, col), Direction.UP, WallState.WALL)
            if row == INTERIOR_ROWS:
                board.set_wall((row, col), Direction.DOWN, WallState.WALL)
            if col == 1:
                board.set_wall((row, col), Direction.LEFT, WallState.WALL)
            if col == INTERIOR_COLS:
                board.set_wall((row, col), Direction.RIGHT, WallState.WALL)


# Tablero inicial de esta simulación: reproduce la configuración de
# arranque de la casa del tablero oficial de Flash Point (planos de 8
# cuartos - sala, baño, recámara, pasillo, cocina, cuarto de niños,
# comedor, sala de música/recámara). Todo queda fijo aquí en código -
# no se lee ningún archivo externo.
#
# Cuartos (fila 1-6, columna 1-8):
#   Sala (cols 1-3) | Baño (col 4) | Recámara (cols 5-8)         <- filas 1-2
#   Pasillo (cols 1-2) | Cocina (cols 3-6) | Cuarto niños (cols 7-8)  <- filas 3-4
#   Comedor (cols 1-2) | Sala de música (cols 3-6) | Recámara (cols 7-8) <- filas 5-6
INITIAL_POI_POSITIONS = [(2, 4), (5, 1), (5, 8)]


def create_board():
    """Arma el tablero con el que arranca la simulación: paredes,
    puertas y el fuego inicial (10 marcadores, como en el "Family
    Game Setup"). Los 3 POI van en `INITIAL_POI_POSITIONS`; su
    identidad (víctima o falsa alarma) no se fija aquí - la decide al
    azar `FirefighterModel` al arrancar, igual que voltear un marcador
    con el lado "?" hacia arriba en el juego físico."""
    board = Board()
    _seal_building_perimeter(board)

    # --- paredes entre cuartos -------------------------------------------
    # (verificado a mano contra el instructivo del juego)
    interior_walls = [
        ((2, 3), Direction.RIGHT), ((1, 5), Direction.RIGHT),
        ((4, 2), Direction.RIGHT), ((3, 6), Direction.RIGHT),
        ((5, 5), Direction.RIGHT), ((4, 6), Direction.RIGHT),
        ((2, 3), Direction.DOWN), ((2, 4), Direction.DOWN), ((2, 5), Direction.DOWN),
        ((2, 6), Direction.DOWN), ((2, 7), Direction.DOWN),
        ((4, 1), Direction.DOWN), ((4, 2), Direction.DOWN), ((4, 3), Direction.DOWN),
        ((4, 5), Direction.DOWN), ((4, 8), Direction.DOWN), ((4, 7), Direction.DOWN),
        ((5, 7), Direction.RIGHT), ((6, 5), Direction.DOWN),
    ]
    for pos, direction in interior_walls:
        board.set_wall(pos, direction, WallState.WALL)

    # --- puertas -----------------------------------------------------------
    # (verificado a mano contra el instructivo del juego)
    doors = [
        # interiores
        ((1, 4), Direction.LEFT, WallState.DOOR_CLOSE),
        ((2, 5), Direction.RIGHT, WallState.DOOR_CLOSE),
        ((3, 2), Direction.RIGHT, WallState.DOOR_CLOSE),
        ((2, 8), Direction.DOWN, WallState.DOOR_CLOSE),
        ((4, 6), Direction.DOWN, WallState.DOOR_CLOSE),
        ((4, 4), Direction.DOWN, WallState.DOOR_CLOSE),
        ((6, 7), Direction.RIGHT, WallState.DOOR_CLOSE),
        ((6, 5), Direction.RIGHT, WallState.DOOR_CLOSE),
        ((4, 7), Direction.LEFT, WallState.DOOR_CLOSE),
        # exteriores
        ((1, 6), Direction.UP, WallState.DOOR_OPEN),
        ((3, 1), Direction.LEFT, WallState.DOOR_OPEN),
        ((4, 8), Direction.RIGHT, WallState.DOOR_OPEN),
        ((6, 3), Direction.DOWN, WallState.DOOR_OPEN),
    ]
    for pos, direction, state in doors:
        board.set_wall(pos, direction, state)

    # --- fuego inicial (10 marcadores) -------------------------------------
    fires = [
        (2, 2), (2, 3), (3, 2), (3, 3), (3, 4),
        (3, 5), (4, 4), (5, 6), (5, 7), (6, 6),
    ]
    for pos in fires:
        board.set_state(pos, CellState.FIRE)

    return board


if __name__ == "__main__":
    b = create_board()
    b.dump()
