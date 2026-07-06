using System.Collections.Generic;
using UnityEngine;

// Player-height "dollhouse" for multi-floor interiors (first use: the Salone
// delle Arti gallery). While the player is on the ground floor, everything
// named with the level prefix ("Level2_", children included) hides so the
// pitched HD-2D camera can look down into the hall; once the player climbs
// past the threshold the upper floor shows again. This is the Duomo dollhouse
// idiom generalized from camera-ray occlusion to player height.
//
// Only renderers are toggled — colliders always stay live, so the gallery
// floor is solid even while invisible. The name prefix is LOAD-BEARING:
// builders and prop placement must keep upper-level content under a
// "Level2_"-named root.
public class LevelVisibilityFader : MonoBehaviour
{
    [Tooltip("GameObjects whose name starts with this (plus their children) belong to the upper level.")]
    public string levelPrefix = "Level2_";
    public string playerTag = "Player";
    [Tooltip("Player world Y above which the upper level is shown.")]
    public float showAboveY = 2.5f;
    [Tooltip("Hysteresis band so mid-stair hovering doesn't flicker the toggle.")]
    public float hysteresis = 0.5f;

    Transform _player;
    readonly List<Renderer> _upper = new List<Renderer>();
    bool _visible = true;

    void Start()
    {
        Collect();
        Apply(GetPlayerY() > showAboveY, force: true);
    }

    void Collect()
    {
        _upper.Clear();
        var seen = new HashSet<Renderer>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(levelPrefix)) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    if (seen.Add(r)) _upper.Add(r);
            }
        }
    }

    float GetPlayerY()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) _player = go.transform;
        }
        return _player != null ? _player.position.y : 0f;
    }

    void LateUpdate()
    {
        float y = GetPlayerY();
        if (_visible && y < showAboveY - hysteresis) Apply(false);
        else if (!_visible && y > showAboveY) Apply(true);
    }

    void Apply(bool visible, bool force = false)
    {
        if (!force && visible == _visible) return;
        _visible = visible;
        foreach (var r in _upper)
            if (r != null) r.enabled = visible;
    }
}
