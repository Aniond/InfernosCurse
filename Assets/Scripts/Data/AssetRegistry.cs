using System.Collections.Generic;
using UnityEngine;

// Runtime name→asset lookup for save-game restore. Skills and jobs live
// outside Resources/, so loading a save needs a pre-wired registry to turn
// persisted asset names back into ScriptableObject references. Lives on the
// GameSystems prefab; populated by the editor menu item
// InfernosCurse/Battle Integration/4. Populate Asset Registry.
public class AssetRegistry : MonoBehaviour
{
    [Tooltip("Every SkillDefinition in Assets/Data (wired by the setup menu).")]
    public SkillDefinition[] allSkills;
    [Tooltip("Every JobDefinition in Assets/Data (wired by the setup menu).")]
    public JobDefinition[] allJobs;

    // Lazy-resolving singleton (house pattern — mid-play-reload safe).
    static AssetRegistry _instance;
    public static AssetRegistry Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<AssetRegistry>());
        private set => _instance = value;
    }

    Dictionary<string, SkillDefinition> _skills;
    Dictionary<string, JobDefinition> _jobs;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void BuildLookups()
    {
        if (_skills != null) return;
        _skills = new Dictionary<string, SkillDefinition>();
        _jobs = new Dictionary<string, JobDefinition>();
        if (allSkills != null)
            foreach (var s in allSkills)
                if (s != null && !_skills.ContainsKey(s.name)) _skills[s.name] = s;
        // New content may live under Resources so it can be built without
        // rewriting the shared GameSystems prefab. Explicit registry entries
        // still win; resource-backed additions fill only missing names.
        foreach (var s in Resources.LoadAll<SkillDefinition>("Skills"))
            if (s != null && !_skills.ContainsKey(s.name)) _skills[s.name] = s;
        if (allJobs != null)
            foreach (var j in allJobs)
                if (j != null && !_jobs.ContainsKey(j.name)) _jobs[j.name] = j;
    }

    public SkillDefinition FindSkill(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        BuildLookups();
        _skills.TryGetValue(assetName, out var s);
        if (s == null) Debug.LogWarning($"[AssetRegistry] Unknown skill '{assetName}' in save data.");
        return s;
    }

    public JobDefinition FindJob(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;
        BuildLookups();
        _jobs.TryGetValue(assetName, out var j);
        if (j == null) Debug.LogWarning($"[AssetRegistry] Unknown job '{assetName}' in save data.");
        return j;
    }
}
