import networkx as nx
from mesa import Agent

from .board import Board, CellState, Direction, PoiState, WallState


STARTING_AP = 4
MAX_SAVED_AP = 4
MOVE_COST = 1
MOVE_INTO_FIRE_COST = 2
CARRY_VICTIM_COST = 2
DOOR_COST = 1
CHOP_COST = 2
EXTINGUISH_SMOKE_COST = 1
EXTINGUISH_FIRE_COST = 2
CHOP_DAMAGE_LIMIT = 10  # con el edificio ya tan dañado, no vale la pena arriesgarse a hachar más

_MAX_ACTIONS_PER_TURN = 20  # tope de seguridad contra bucles infinitos


class Firefighter(Agent):
    """Un bombero (agente de Mesa). No tiene rol fijo - cada uno se
    maneja solo, con un presupuesto de Puntos de Acción (AP) por turno
    que gasta en moverse, apagar fuego, abrir/cerrar puertas o hachar
    paredes."""

    def __init__(self, model, pos: tuple[int, int]):
        super().__init__(model)
        self.pos = pos
        self.ap = 0
        self.saved_ap = 0
        self.carrying = False
        self.knocked_down = False

    @property
    def board(self) -> Board:
        """Atajo para llegar al tablero desde el modelo."""
        return self.model.board

    # ---- funciones auxiliares -----------------------------------------

    def _neighbor(self, direction: Direction) -> tuple[int, int] | None:
        """Devuelve la celda vecina en esa dirección (o None si se sale
        del tablero)."""
        return self.board.neighbor(self.pos, direction)

    def _spend(self, cost: int) -> bool:
        """Intenta gastar `cost` AP. Si no alcanza, no hace nada y
        devuelve False; si alcanza, lo descuenta y devuelve True."""
        if self.ap < cost:
            return False
        self.ap -= cost
        return True

    def _direction_to(self, target: tuple[int, int]) -> Direction | None:
        """Averigua en qué dirección (arriba/abajo/izq/der) está `target`
        respecto a la posición actual, si es una celda vecina."""
        for direction in Direction:
            if self.board.neighbor(self.pos, direction) == target:
                return direction
        return None

    def _exterior_cells(self) -> set[tuple[int, int]]:
        """Todas las celdas de afuera del edificio (el anillo exterior
        del tablero) - el destino de un bombero que carga una víctima."""
        return {
            (r, c)
            for r in range(self.board.height)
            for c in range(self.board.width)
            if self.board.is_outside((r, c))
        }

    def _passable_graph(self, avoid_fire: bool = True) -> nx.Graph:
        """Una copia del grafo del tablero que solo conserva las aristas
        por las que hoy se puede pasar (sin pared, o puerta abierta).
        Si `avoid_fire` es True, además quita las celdas en llamas -
        nunca conviene planear una ruta que atraviese fuego pudiendo
        rodearlo. Se usa para calcular caminos hacia un objetivo."""
        blocked_edges = [
            (u, v)
            for u, v, data in self.board.G.edges(data=True)
            if data["wall"] not in (WallState.CLEAR, WallState.DOOR_OPEN)
        ]
        graph = self.board.G.copy()
        graph.remove_edges_from(blocked_edges)
        if avoid_fire:
            fire_cells = [
                pos
                for pos, data in self.board.G.nodes(data=True)
                if data["state"] == CellState.FIRE and pos != self.pos
            ]
            graph.remove_nodes_from(fire_cells)
        return graph

    def _shortest_path_to(self, targets: set[tuple[int, int]]) -> list[tuple[int, int]] | None:
        """Busca, entre todas las celdas en `targets`, el camino más
        corto desde la posición actual (respetando paredes/puertas
        cerradas). Primero intenta sin pasar por ninguna celda en
        llamas; si así no hay ruta a NINGÚN objetivo, vuelve a intentar
        permitiendo cruzar fuego, para no quedarse parado sin hacer
        nada cuando esa es la única forma de llegar. Devuelve la lista
        de celdas del camino, o None si ningún objetivo es alcanzable
        de ninguna forma."""
        if not targets:
            return None
        for avoid_fire in (True, False):
            graph = self._passable_graph(avoid_fire=avoid_fire)
            if self.pos not in graph:
                continue
            best_path = self._best_path_in_graph(graph, targets)
            if best_path is not None:
                return best_path
        return None

    def _best_path_in_graph(self, graph: nx.Graph, targets: set[tuple[int, int]]) -> list[tuple[int, int]] | None:
        """El camino más corto dentro de `graph` desde la posición
        actual hasta cualquiera de `targets`."""
        best_path = None
        for target in targets:
            if target not in graph:
                continue
            try:
                path = nx.shortest_path(graph, self.pos, target)
            except nx.NetworkXNoPath:
                continue
            if best_path is None or len(path) < len(best_path):
                best_path = path
        return best_path

    def _open_a_closed_door(self) -> bool:
        """Si hay una puerta cerrada en alguno de los 4 lados de la
        celda actual, la abre (gastando AP) y devuelve True. Se usa
        cuando el camino hacia el objetivo está bloqueado por una
        puerta cerrada."""
        for direction in Direction:
            if self.board.get_wall(self.pos, direction) == WallState.DOOR_CLOSE:
                if self.open_close_door(direction):
                    return True
        return False

    # ---- acciones del juego ----------------------------------------------

    def move(self, direction: Direction, carry: bool = False) -> bool:
        """Mueve al bombero una celda en `direction`. Si `carry=True` (o
        ya venía cargando), intenta llevarse consigo a la víctima que
        esté en su celda actual: cuesta más AP y no puede entrar a una
        celda en llamas. Al llegar a una celda con un POI sin revelar lo
        revela automáticamente (costo 0); si sale del edificio cargando
        una víctima, la rescata."""
        target = self._neighbor(direction)
        if target is None or not self.board.is_passable(self.pos, direction):
            return False

        carrying_now = self.carrying or carry
        if carrying_now:
            poi = self.board.poi_at(self.pos)
            if poi is None or poi.kind != PoiState.VICTIM or not poi.revealed:
                carrying_now = False

        state = self.board.get_state(target)
        if carrying_now and state == CellState.FIRE:
            return False  # no se puede cargar una víctima hacia una celda en fuego

        if carrying_now:
            cost = CARRY_VICTIM_COST
        elif state == CellState.FIRE:
            cost = MOVE_INTO_FIRE_COST
        else:
            cost = MOVE_COST

        if not self._spend(cost):
            return False

        old_pos = self.pos
        self.pos = target

        if carrying_now:
            self.board.move_victim(old_pos, target)
            self.carrying = True
            if self.board.is_outside(target):
                self.model.rescue_victim(target)
                self.carrying = False
        elif self.board.has_poi(target):
            poi = self.board.poi_at(target)
            if not poi.revealed:
                kind = self.board.reveal_poi(target)
                if kind == PoiState.FALSE:
                    self.model.identify_false_alarm(target)
        return True

    def open_close_door(self, direction: Direction) -> bool:
        """Abre o cierra la puerta que está en ese lado de la celda
        actual (según cómo esté, hace lo contrario)."""
        wall = self.board.get_wall(self.pos, direction)
        if wall not in (WallState.DOOR_OPEN, WallState.DOOR_CLOSE):
            return False
        if not self._spend(DOOR_COST):
            return False
        new_state = WallState.DOOR_CLOSE if wall == WallState.DOOR_OPEN else WallState.DOOR_OPEN
        self.board.set_wall(self.pos, direction, new_state)
        return True

    def extinguish(self, direction: Direction | None = None) -> bool:
        """Apaga fuego o humo. direction=None apunta a la propia celda
        del bombero; si no, a la celda vecina en esa dirección. Humo
        cuesta menos que fuego."""
        target = self.pos if direction is None else self._neighbor(direction)
        if target is None:
            return False

        state = self.board.get_state(target)
        if state == CellState.SMOKE:
            if not self._spend(EXTINGUISH_SMOKE_COST):
                return False
            self.board.set_state(target, CellState.CLEAR)
            return True
        if state == CellState.FIRE:
            if not self._spend(EXTINGUISH_FIRE_COST):
                return False
            self.board.set_state(target, CellState.CLEAR)
            return True
        return False

    def chop(self, direction: Direction) -> bool:
        """Hacha la pared de ese lado de la celda actual: el primer
        golpe la deja Dañada, el segundo la Destruye (queda pasable).
        Cada golpe cuenta como un punto de daño para el edificio - por
        eso, si el edificio ya acumuló `CHOP_DAMAGE_LIMIT` o más puntos
        de daño, ningún bombero hacha paredes (ni buscando otra ruta ni
        aunque sea la única forma de pasar): ya está demasiado cerca
        del colapso para arriesgarse a sumarle más."""
        if self.model.damage_total >= CHOP_DAMAGE_LIMIT:
            return False
        wall = self.board.get_wall(self.pos, direction)
        if wall not in (WallState.WALL, WallState.DAMAGED_WALL):
            return False
        if not self._spend(CHOP_COST):
            return False
        new_state = WallState.CLEAR if wall == WallState.DAMAGED_WALL else WallState.DAMAGED_WALL
        self.board.set_wall(self.pos, direction, new_state)
        self.model.register_damage(1)
        return True

    # ---- turno -----------------------------------------------------------

    def take_turn(self):
        """Juega el turno completo de este bombero: le da su AP (los 4
        de siempre más lo que tenía guardado de turnos anteriores) y
        deja que la estrategia decida qué hacer con ellos. Si quedó AP
        sin gastar al final, se guarda (hasta un máximo de 4) para el
        próximo turno."""
        if self.model.status != "in_progress":
            return
        if self.knocked_down:
            self.knocked_down = False  # este turno se lo pasa recuperándose, según las reglas
            return

        self.ap = STARTING_AP + self.saved_ap
        self.saved_ap = 0

        self._act()

        self.saved_ap = min(self.ap, MAX_SAVED_AP)
        self.ap = 0

    # ---- estrategia --------------------------------------------------------

    def _act(self):
        """La estrategia: cada bombero se maneja solo, sin rol fijo.
        En cada momento del turno, la prioridad es:
        1) Apagar el fuego/humo que tenga encima o al lado - nunca lo
           deja pasar de largo, así vaya hacia otro lado.
        2) Si va cargando una víctima (o la que tiene debajo ya se
           reveló como víctima confirmada), dirigirse a la salida más
           cercana.
        3) Si no, dirigirse al POI más cercano - de preferencia uno que
           todavía no esté junto al fuego/humo; si todos están en
           riesgo, va tras el más cercano de cualquier forma.
        Como el paso 1 se revisa en cada vuelta del bucle, un bombero
        de camino a un POI apaga solo, sobre la marcha, cualquier
        fuego/humo con el que se vaya topando. Repite hasta quedarse
        sin AP o sin nada útil que hacer."""
        for _ in range(_MAX_ACTIONS_PER_TURN):
            if self.ap <= 0 or self.model.status != "in_progress":
                return

            if self.extinguish(None):
                continue
            if self._extinguish_a_neighbor():
                continue

            poi_here = self.board.poi_at(self.pos)
            if poi_here is not None and not poi_here.revealed:
                self.board.reveal_poi(self.pos)  # revelar un POI cuesta 0 AP
                poi_here = self.board.poi_at(self.pos)  # desaparece si era falsa alarma
                if poi_here is None:
                    self.model.identify_false_alarm(self.pos)

            should_carry = self.carrying or (poi_here is not None and poi_here.kind == PoiState.VICTIM)
            if should_carry:
                targets = self._exterior_cells()
            else:
                all_pois = {p.pos for p in self.board.pois}
                safe_pois = {pos for pos in all_pois if not self._is_endangered(pos)}
                targets = safe_pois if safe_pois else all_pois
                if not targets:
                    # no hay ningún POI conocido en este instante: mejor
                    # ir a apagar el fuego más cercano que quedarse parado
                    targets = self._approach_cells(self._fire_smoke_cells())

            if not self._step_towards(targets, carry=should_carry):
                return

    def _extinguish_a_neighbor(self) -> bool:
        """Si alguna celda vecina tiene fuego o humo, la apaga ahí
        mismo (sin moverse) y devuelve True."""
        for direction in Direction:
            neighbor = self._neighbor(direction)
            if neighbor is not None and self.board.get_state(neighbor) in (CellState.FIRE, CellState.SMOKE):
                if self.extinguish(direction):
                    return True
        return False

    def _fire_smoke_cells(self) -> set[tuple[int, int]]:
        """Todas las celdas del tablero que ahora mismo tienen fuego o
        humo."""
        return {
            pos
            for pos, data in self.board.G.nodes(data=True)
            if data["state"] in (CellState.FIRE, CellState.SMOKE)
        }

    def _approach_cells(self, cells: set[tuple[int, int]]) -> set[tuple[int, int]]:
        """Las celdas desde las que se puede actuar sobre cualquiera de
        `cells` sin pisar fuego: la propia celda (si no está en llamas)
        y sus vecinas."""
        result = set()
        for pos in cells:
            if self.board.get_state(pos) != CellState.FIRE:
                result.add(pos)
            for direction in Direction:
                neighbor = self.board.neighbor(pos, direction)
                if neighbor is not None:
                    result.add(neighbor)
        return result

    def _is_endangered(self, pos: tuple[int, int]) -> bool:
        """¿Esa celda ya tiene fuego/humo, o lo tiene alguna vecina?
        Un POI ahí puede quemarse pronto."""
        if self.board.get_state(pos) in (CellState.FIRE, CellState.SMOKE):
            return True
        for direction in Direction:
            neighbor = self.board.neighbor(pos, direction)
            if neighbor is not None and self.board.get_state(neighbor) in (CellState.FIRE, CellState.SMOKE):
                return True
        return False

    def _step_towards(self, targets: set[tuple[int, int]], carry: bool = False) -> bool:
        """Avanza un paso por el camino más corto conocido hacia
        `targets`; si el paso está bloqueado por una puerta cerrada, la
        abre en su lugar. Devuelve False cuando ya no hay nada útil que
        hacer este turno (sin camino, o completamente bloqueado)."""
        path = self._shortest_path_to(targets)
        if not path or len(path) < 2:
            return False

        direction = self._direction_to(path[1])
        if direction is not None and self.move(direction, carry=carry):
            return True

        return self._open_a_closed_door()
