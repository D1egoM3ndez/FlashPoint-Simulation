// TopDownCamera.cs
// Camara ortografica top-down con pan (clic derecho + arrastrar) y
// zoom (rueda del mouse). Pegar en la Main Camera.
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
        cam.orthographic = true;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // mirando hacia abajo
    }

    // Centra la camara sobre el tablero y calcula el Size exacto para
    // que quepa completo en la vista, sin importar el tamano del
    // tablero ni el aspecto de la pantalla. Se llama una vez desde
    // BoardBuilder despues de instanciar todo.
    public void FitToBoard(int widthCells, int heightCells, float cellSize)
    {
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

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
    }
}
