using UnityEngine;

// Marker on the persistent GameSystems prefab root. GameSystemsBootstrap uses
// the presence of this component to decide whether the systems are already
// spawned — independent of which individual systems (HubMap, etc.) live on it.
public class GameSystemsRoot : MonoBehaviour { }
