using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Script-spawns GuildInteractionZones into explorable scenes on load, so big
// scene files never need editing to gain an inn door or a chapel (the
// MercatoVecchio corruption incident is why this authoring surface exists).
// Lives on the GameSystems prefab; entries are authored in the inspector.
public class GuildInteractionSpawner : MonoBehaviour
{
    [Serializable]
    public class ZoneEntry
    {
        public string sceneName = "MercatoVecchio";
        public GuildInteractionZone.Kind kind = GuildInteractionZone.Kind.Inn;
        public string guildId = "albergatori";
        public string label = "Inn";
        public Vector3 position;
        public Vector3 triggerSize = new Vector3(2.5f, 2f, 2.5f);
        public int innPrice = 10;
        public bool isGuildInn = true;
    }

    public List<ZoneEntry> entries = new List<ZoneEntry>();

    readonly List<GameObject> _spawned = new List<GameObject>();

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnFor(SceneManager.GetActiveScene().name);
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) SpawnFor(scene.name);
    }

    void SpawnFor(string sceneName)
    {
        // Old spawns die with their scene; just forget the handles.
        _spawned.RemoveAll(go => go == null);

        int count = 0;
        foreach (var e in entries)
        {
            if (e.sceneName != sceneName) continue;

            var go = new GameObject($"[GuildZone] {e.kind} - {e.label}");
            go.transform.position = e.position;
            var col = go.AddComponent<BoxCollider>();
            col.size = e.triggerSize;
            col.isTrigger = true;

            var zone = go.AddComponent<GuildInteractionZone>();
            zone.kind = e.kind;
            zone.guildId = e.guildId;
            zone.label = e.label;
            zone.innPrice = e.innPrice;
            zone.isGuildInn = e.isGuildInn;

            _spawned.Add(go);
            count++;
        }
        if (count > 0)
            Debug.Log($"[GuildSpawner] {count} zones in {sceneName}");
    }
}
