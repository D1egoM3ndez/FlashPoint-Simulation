from .model import FirefighterModel


def run_simulation() -> FirefighterModel:
    """Corre una partida completa (desde cero hasta ganar/perder) sobre
    el tablero inicial fijo de la simulación, imprime un resumen del
    resultado, y devuelve el modelo ya terminado por si se quiere
    inspeccionar más."""
    model = FirefighterModel()
    status = model.run()
    print(f"Simulación terminada: {status}")
    print(f"  Turnos jugados: {model.turn}")
    print(f"  Víctimas rescatadas: {model.victims_rescued}")
    print(f"  Víctimas perdidas: {model.victims_lost}")
    print(f"  Daño total: {model.damage_total}")
    return model


def main():
    """Punto de entrada del comando `fire` (ver `pyproject.toml`):
    corre una partida nueva."""
    run_simulation()
