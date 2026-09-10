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
    [HideInInspector] public bool loadFailed = false;
    [HideInInspector] public string loadStatus = "";

    public void LoadWithSeed(int seed)       { BeginLoad(seed, false); }
    public void LoadWithSeedRandom(int seed) { BeginLoad(seed, true); }

    void BeginLoad(int seed, bool useRandom)
    {
        StopAllCoroutines();
        isLoaded = false;
        loadFailed = false;
        loadStatus = "";
        turns.Clear();
        StartCoroutine(LoadFromServer(seed, useRandom));
    }

    IEnumerator LoadFromServer(int seed, bool useRandom)
    {
        string endpoint = useRandom ? "/run-sim-rand" : "/run-sim";
        string url = $"{serverUrl}{endpoint}?seed={seed}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LoadFromFile();
                yield break;
            }

            turns = BoardDataConverter.ParseJsonArray(request.downloadHandler.text);

            if (turns.Count == 0)
            {
                LoadFromFile();
                yield break;
            }

            isLoaded = true;
        }
    }

    void LoadFromFile()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName + ".json");

        if (!File.Exists(path))
        {
            Fail($"No hay server y no existe {path}");
            return;
        }

        turns = BoardDataConverter.ParseJsonArray(File.ReadAllText(path));

        if (turns.Count == 0)
        {
            Fail($"{fileName}.json no se pudo parsear (0 turnos).");
            return;
        }

        isLoaded = true;
    }

    void Fail(string reason)
    {
        loadFailed = true;
        loadStatus = reason;
    }

    public BoardData GetTurn(int index)
    {
        if (index < 0 || index >= turns.Count)
            return null;
        return turns[index];
    }

    public int TurnCount => turns.Count;
}
