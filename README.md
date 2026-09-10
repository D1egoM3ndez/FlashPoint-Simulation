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

**General**

- La documentación de funciones, métodos y clases fue generada con IA.
- Las convenciones de nombres, así como el `_` para métodos privados, fueron
  sugeridas por la IA.

**Scripts de Unity (`Assets/Scripts/`)**

- JsonLoader y JsonUtility: Investigación hecha con IA para cubrir todos los aspectos y posibles errores que pudieran surgir a la hora de conectarse con el servidor
- UIManager: Ajuste de botones en código y ayuda en los botones para pasar de step (turno) en la simulación.

**Arte**
Uso de IA para la generación de la pantalla de juego principal

**Assets**
[Kenney](https://kenney.nl/assets/graveyard-kit)
