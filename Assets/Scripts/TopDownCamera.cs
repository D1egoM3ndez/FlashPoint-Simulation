// TopDownCamera.cs
// Pan (clic derecho + arrastrar) y zoom (rueda del mouse) para la camara del tablero.
// La proyeccion (ortografica/perspectiva), rotacion y posicion inicial se dejan como
// esten configuradas en el Inspector (por ahora: perspectiva, en angulo) en vez de
// forzarlas a top-down desde el script.
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TopDownCamera : MonoBehaviour
{
    public float panSpeed = 0.05f;
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 25f;
    [Tooltip("Margen extra alrededor del tablero al encuadrarlo (1 = justo, 1.1 = 10% de aire)")]
    public float padding = 1.1f;

    Camera cam;
    Vector3 lastMousePosition;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    // Centra la camara sobre el tablero y calcula el Size exacto para que quepa
    // completo en la vista. Solo aplica si la camara esta en modo ortografico
    // (top-down); si esta en perspectiva (encuadre manual en el Inspector) no toca
    // nada, para no pisar la posicion/rotacion que hayas dejado configurada ahi.
    public void FitToBoard(int widthCells, int heightCells, float cellSize)
    {
        if (!cam.orthographic) return;

        float worldWidth = widthCells * cellSize;
        float worldHeight = heightCells * cellSize;

        float sizeForHeight = worldHeight / 2f;
        float sizeForWidth = worldWidth / (2f * cam.aspect);
        float fitSize = Mathf.Max(sizeForHeight, sizeForWidth) * padding;

        cam.orthographicSize = fitSize;
        minZoom = fitSize * 0.3f;   // no dejar acercarse demasiado
        maxZoom = fitSize * 2f;     // ni alejarse demasiado

        Vector3 center = new Vector3(worldWidth / 2f - cellSize / 2f, 20f, -(worldHeight / 2f - cellSize / 2f));
        transform.position = center;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // mirando hacia abajo
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            transform.position += new Vector3(-delta.x, 0, -delta.y) * panSpeed;
            lastMousePosition = Input.mousePosition;
        }

        if (!cam.orthographic) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
    }
}
