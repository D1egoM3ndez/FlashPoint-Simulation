<<<<<<< HEAD
# Flashpoint Simulación

Simulación basada en el juego Flash Point: Fire Rescue. La lógica multiagente está
construida con [Mesa](https://mesa.readthedocs.io/) y la visualización 3D con
[Unity](https://unity.com/). El puente entre ambas es un JSON turno por turno que
la simulación exporta y Unity reproduce (servido con
[Flask](https://flask.palletsprojects.com/) o leído de un archivo local).

Para la materia TC2008B, el reto final.

## Requisitos

- Unity 6 (`6000.5.7f1`) para la visualización
- Python 3.13+ para la simulación (dependencias: `mesa`, `networkx`, `numpy`)

Se recomienda utilizar `uv` para evitar problemas de versiones y dependencias.

- [uv](https://docs.astral.sh/uv/)

## Instalación

```bash
git clone <repo> && cd Flash_Point_Simulation
```

Para la simulación en Python (desde `src/`):

```bash
uv venv && uv pip install mesa networkx numpy
```

Para la visualización, abrir la carpeta del proyecto con Unity Hub.

## Ejecución

### Simulación (Mesa)

Corre una partida completa e imprime el resultado (turnos, víctimas
rescatadas/perdidas, daño total):

```bash
cd src
python -m game
```

### Visualización (Unity)

1. Abrir el proyecto con Unity `6000.5.7f1`.
2. Abrir la escena `Assets/Scenes/SampleScene.unity`.
3. Play. `JsonLoader` intenta obtener la secuencia de turnos del servidor Flask de
   la simulación y, si no responde, usa `Assets/StreamingAssets/game.json` como
   respaldo.

En la pantalla de inicio se elige el tipo de agente (estrategia mejorada o
aleatoria); los controles de playback permiten avanzar y retroceder turnos.

## Estructura del proyecto

```
src/game/            - Simulación multiagente (Mesa)
  model.py           - Modelo principal (FirefighterModel): motor de la partida
  agents.py          - Agente bombero (Firefighter) con estrategia coordinada
  board.py           - Tablero: celdas, paredes, puertas y POIs
  fire_rules.py      - Propagación del fuego, explosiones, flashover y reposición de POIs
  __init__.py        - Punto de entrada (run_simulation / main)

Assets/
  Scripts/
    GameManager.cs   - Playback: navega los turnos del JSON y los renderiza con animación
    JsonLoader.cs    - Carga la secuencia de turnos del servidor Flask o de StreamingAssets
    BoardData.cs     - Formato interno + parser del JSON de la simulación
    BoardBuilder.cs  - Construye el tablero 3D (pisos, muros, puertas, fuego/humo, POIs, agentes)
    UIManager.cs     - HUD: turno actual, estadísticas y botonera de playback
    StartScreen.cs   - Pantalla de inicio: elegir estrategia mejorada o aleatoria
    PauseMenu.cs     - Menú de pausa (ESC)
    TopDownCamera.cs - Cámara del tablero: paneo y zoom
  StreamingAssets/
    game.json        - Secuencia de turnos de respaldo (si no hay servidor)
  Scenes/, Prefabs/, Models/, Materials/, Animations/ - Recursos de la escena

Diagramas_de_estado/ - Diagramas de estado (PlantUML): juego, celdas, agentes, paredes y POIs
Multiagentes.pdf     - Enunciado del reto
```

## Agentes

- **Smart** (estrategia mejorada): asigna víctimas por proximidad, prioriza la
  supresión de fuego y el rescate de forma coordinada.
- **Random**: toma decisiones aleatorias (línea base comparativa).

## Condiciones de victoria/derrota

- **Victoria**: rescatar >= 7 víctimas
- **Derrota**: perder >= 4 víctimas **o** daño total >= 24 (colapso del edificio)

## Uso de IA

Algunas partes de este proyecto fueron generadas con ayuda de la IA. A
continuación se mencionan estas partes.

Sin embargo, cabe recalcar que las estrategias, algoritmos, y lógica principal no
fue generada con IA.

**General**

- La documentación de funciones, métodos y clases fue generada con IA.
- Las convenciones de nombres, así como el `_` para métodos privados, fueron
  sugeridas por la IA.

**`model.py`**

- <describir aquí la ayuda puntual de IA, p. ej. bugs resueltos o argumentos sugeridos>

**`agents.py`**

- <describir aquí la ayuda puntual de IA>

**`board.py`**

- <describir aquí la ayuda puntual de IA>

**`fire_rules.py`**

- <describir aquí la ayuda puntual de IA>

**Scripts de Unity (`Assets/Scripts/`)**

- <describir aquí la ayuda puntual de IA en el parser de JSON, playback, HUD, etc.>

**`README.md`**

- La IA se utilizó como ayuda para redactar este README.
=======
Hello World
>>>>>>> c6fd32b36572ffa326286198bd594b9d48d0e547
