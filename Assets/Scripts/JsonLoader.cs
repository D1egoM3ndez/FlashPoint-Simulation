// JsonLoader.cs
// Carga JSON desde el server Flask o desde StreamingAssets como fallback.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class JsonLoader : MonoBehaviour
{
    [Header("Server Flask")]
    public string serverUrl = "http://localhost:5000";

    [Header("Fallback: archivo local")]
    public string fileName = "game";

    [HideInInspector] public List<BoardData> turns = new List<BoardData>();
    [HideInInspector] public bool isLoaded = false;

    public void LoadWithSeed(int seed)
    {
        StartCoroutine(LoadFromServer(seed));
    }

    IEnumerator LoadFromServer(int seed)
    {
        string url = $"{serverUrl}/run-sim?seed={seed}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                LoadFromFile();
                yield break;
            }

            string json = request.downloadHandler.text;
            turns = BoardDataConverter.ParseJsonArray(json);

            if (turns.Count == 0)
                yield break;

            isLoaded = true;
        }
    }

    void LoadFromFile()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName + ".json");

        if (!File.Exists(path))
            return;

        string json = File.ReadAllText(path);
        turns = BoardDataConverter.ParseJsonArray(json);

        if (turns.Count == 0)
            return;

        isLoaded = true;
    }

    public BoardData GetTurn(int index)
    {
        if (index < 0 || index >= turns.Count)
            return null;
        return turns[index];
    }

    public int TurnCount => turns.Count;
}
