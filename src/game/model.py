from mesa import Model

from .agents import Firefighter
from .board import INITIAL_POI_POSITIONS, PoiState, create_board
from .fire_rules import FireEvent, advance_fire, replenish_poi

# Conteos del "Family Game Setup" (Reto AD 2026): estos pools son lo que
# queda en la caja después de quitar 2 Víctimas y 1 Falsa alarma, menos
# los POI que se colocan en `INITIAL_POI_POSITIONS` (ver board.py).
TOTAL_VICTIMS = 10
TOTAL_FALSE_ALARMS = 5
POI_TARGET_ON_BOARD = 3

VICTIMS_TO_WIN = 7
VICTIMS_LOST_TO_LOSE = 4
COLLAPSE_DAMAGE = 24


class FirefighterModel(Model):
    """El modelo de Mesa: es el motor completo de la simulación. Arma el
    tablero inicial (ver `create_board` en board.py), crea a los 6
    bomberos, y sabe cómo avanzar la partida turno por turno hasta que
    se gana o se pierde."""

    def __init__(self):
        """Arma una partida nueva: arma el tablero inicial (paredes,
        puertas y fuego fijos en `create_board`, no se lee ningún
        archivo externo), coloca los 3 POI de arranque con una
        identidad sorteada al azar del pool de 10 víctimas / 5 falsas
        alarmas (igual que barajar los marcadores físicos - la
        posición es fija, pero no lo que hay debajo del "?"), y crea a
        los 6 bomberos. Ninguno tiene rol fijo - cada uno decide sus
        acciones solo (ver `Firefighter._act` en agents.py).

        Las posiciones iniciales del fuego y de los POI siempre son las
        mismas (vienen fijas de `create_board`); todo lo demás - cómo
        se propaga el fuego turno a turno, qué identidad sale en cada
        POI, dónde reaparecen los que se reponen - es genuinamente al
        azar en cada partida, sin semilla fija (mesa arma un generador
        nuevo con entropía del sistema operativo cada vez)."""
        super().__init__()

        self.board = create_board()

        self.victim_pool = TOTAL_VICTIMS
        self.false_alarm_pool = TOTAL_FALSE_ALARMS
        for pos in INITIAL_POI_POSITIONS:
            kind = self._draw_poi_kind()
            if kind is not None:
                self.board.add_poi(pos, kind)

        self.damage_total = 0
        self.victims_rescued = 0
        self.victims_lost = 0
        self.false_alarms_found = 0
        self.turn = 0
        self.status = "in_progress"  # "in_progress" | "won" | "lost"

        self.firefighters: list[Firefighter] = []
        self._spawn_firefighters()

    # ---- preparación de la partida -----------------------------------------

    def _spawn_firefighters(self) -> None:
        """Coloca a los 6 bomberos afuera del edificio, repartidos frente
        a las 4 puertas exteriores (ver `create_board` en board.py): 2 en
        la puerta de la izquierda, 2 en la de abajo, 1 en la de la
        derecha, 1 en la de arriba. Sin rol fijo: cada uno se maneja
        solo (ver `Firefighter._act` en agents.py)."""
        starting_positions = [
            (3, 0), (2, 0),  # izquierda, frente a la puerta de (3,1)
            (7, 3), (7, 4),  # abajo, frente a la puerta de (6,3)
            (4, 9),          # derecha, frente a la puerta de (4,8)
            (0, 6),          # arriba, frente a la puerta de (1,6)
        ]
        for pos in starting_positions:
            self.firefighters.append(Firefighter(self, pos))

    def _consume_pool(self, kind: PoiState) -> None:
        """Resta un marcador del pool restante (víctimas o falsas alarmas)
        cada vez que se coloca uno nuevo en el tablero."""
        if kind == PoiState.VICTIM:
            self.victim_pool = max(0, self.victim_pool - 1)
        else:
            self.false_alarm_pool = max(0, self.false_alarm_pool - 1)

    def _draw_poi_kind(self) -> PoiState | None:
        """Saca al azar la identidad de un nuevo marcador POI, ponderada
        por lo que queda en cada pool (igual que barajar los marcadores
        físicos). Devuelve None si ya no quedan marcadores en la caja."""
        total = self.victim_pool + self.false_alarm_pool
        if total <= 0:
            return None
        kind = PoiState.VICTIM if self.rng.integers(0, total) < self.victim_pool else PoiState.FALSE
        self._consume_pool(kind)
        return kind

    # ---- callbacks que usan las acciones de Firefighter ---------------------

    def rescue_victim(self, pos: tuple[int, int]) -> None:
        """Se llama cuando un bombero saca a una víctima cargada fuera del
        edificio: suma un rescate y revisa si eso ya significa victoria."""
        self.victims_rescued += 1
        self.board.remove_poi(pos)
        self._check_end_conditions()

    def identify_false_alarm(self, pos: tuple[int, int]) -> None:
        """Se llama cuando se revela un POI y resulta ser una falsa alarma
        (solo para llevar la cuenta; no afecta victoria/derrota)."""
        self.false_alarms_found += 1

    def register_damage(self, amount: int = 1) -> bool:
        """Punto único por donde pasa cada marcador de daño a una pared,
        ya sea por un Chop (de a un punto a la vez, vía el callback
        `on_damage` que se le pasa a `advance_fire`)o por una Explosión.
        Revisar la condición de fin de juego justo aquí -de inmediato,
        marcador por marcador- es lo que evita que el edificio acumule
        daño de más allá del umbral de colapso: una sola Explosión puede
        dañar paredes en hasta 4 direcciones antes de que alguien se
        diera cuenta de que el contador ya se pasó.
        Devuelve si la partida sigue en curso."""
        self.damage_total += amount
        self._check_end_conditions()
        return self.status == "in_progress"

    def _lose_victim(self) -> None:
        """Suma una víctima perdida y revisa de inmediato si eso significa
        derrota (4 o más perdidas)."""
        self.victims_lost += 1
        self._check_end_conditions()

    # ---- ciclo de turnos ------------------------------------------------------

    def step(self) -> None:
        """Juega el turno completo de UN bombero, siguiendo el orden del
        juego: 1) Tomar Acción (el bombero gasta su AP), 2) Avanzar Fuego
        (se tira el dado y se resuelve lo que pase), 3) Reponer POI (se
        rellenan los marcadores que falten para llegar a 3 en el tablero).
        Cada una de estas fases se salta si la partida ya terminó a mitad
        de la anterior (por ejemplo, si el edificio colapsó durante el
        turno del bombero)."""
        if self.status != "in_progress":
            return

        firefighter = self.firefighters[self.turn % len(self.firefighters)]
        firefighter.take_turn()

        if self.status == "in_progress":
            event = advance_fire(self.board, self.rng, on_damage=self.register_damage)
            self._apply_fire_event(event)

        if self.status == "in_progress":
            firefighter_positions = {f.pos for f in self.firefighters}
            replenish_poi(self.board, self.rng, self._draw_poi_kind, firefighter_positions, POI_TARGET_ON_BOARD)

        self.turn += 1

    def run(self, max_turns: int = 1000) -> str:
        """Juega turnos (uno por bombero, en orden) hasta que la partida
        se gane, se pierda, o se llegue a `max_turns` (límite de
        seguridad para no correr para siempre). Devuelve el estado final."""
        while self.status == "in_progress" and self.turn < max_turns:
            self.step()
        return self.status

    # ---- consecuencias del avance de fuego -------------------------------

    def _apply_fire_event(self, event: FireEvent) -> None:
        """Procesa lo que dejó un Avance de Fuego: cuenta las víctimas
        perdidas por incendio y derriba a cualquier bombero que haya
        quedado parado en una celda que se acaba de incendiar.
        (El daño a paredes de `event.damage_added` ya se aplicó punto por
        punto durante `advance_fire()` mediante el callback `on_damage`,
        así que aquí no se vuelve a sumar - sería contarlo dos veces.)"""
        for pos, kind in event.lost_pois:
            if self.status != "in_progress":
                break
            if kind == PoiState.VICTIM:
                self._lose_victim()
                # Si esa víctima iba en brazos de un bombero (su POI se
                # mueve junto con quien la carga - ver `board.move_victim`),
                # ya se contó aquí como perdida. Que el derribo de abajo no
                # la vuelva a contar por cargar a una víctima que ya no existe.
                for firefighter in self.firefighters:
                    if firefighter.pos == pos and firefighter.carrying:
                        firefighter.carrying = False

        for firefighter in self.firefighters:
            if self.status != "in_progress":
                break
            if firefighter.pos in event.ignited and not firefighter.knocked_down:
                self._knock_down(firefighter)

    def _knock_down(self, firefighter: Firefighter) -> None:
        """Derriba a un bombero: si estaba cargando una víctima, esa
        víctima se pierde; el bombero queda 'noqueado' (pierde su próximo
        turno) y se manda a la celda exterior más cercana a recuperarse."""
        if firefighter.carrying:
            self._lose_victim()
            firefighter.carrying = False
        firefighter.knocked_down = True
        # Las reglas mandan al bombero derribado al Parking Spot de
        # Ambulancia más cercano; los tableros del Family game no modelan
        # esos espacios de vehículo, así que la celda exterior más cercana
        # (distancia en línea recta) hace las veces de reemplazo.
        firefighter.pos = self._nearest_outside_space(firefighter.pos)

    def _nearest_outside_space(self, pos: tuple[int, int]) -> tuple[int, int]:
        """Busca la celda exterior (fuera del edificio) más cercana a
        `pos` en línea recta ('como vuela el cuervo', según las reglas)."""
        row, col = pos
        candidates = [
            (r, c)
            for r in range(self.board.height)
            for c in range(self.board.width)
            if self.board.is_outside((r, c))
        ]
        return min(candidates, key=lambda p: (p[0] - row) ** 2 + (p[1] - col) ** 2)

    def _check_end_conditions(self) -> None:
        """Revisa si ya se cumplió alguna condición de fin de partida:
        victoria (7 víctimas rescatadas) o derrota (4 víctimas perdidas,
        o el edificio colapsó por acumular demasiado daño). No hace nada
        si la partida ya había terminado antes (para no pisar el
        resultado con una revisión posterior)."""
        if self.status != "in_progress":
            return
        if self.victims_rescued >= VICTIMS_TO_WIN:
            self.status = "won"
        elif self.victims_lost >= VICTIMS_LOST_TO_LOSE:
            self.status = "lost"
        elif self.damage_total >= COLLAPSE_DAMAGE:
            self.status = "lost"
