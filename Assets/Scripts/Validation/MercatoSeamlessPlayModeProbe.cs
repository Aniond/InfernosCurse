#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MercatoSeamlessPlayModeProbe : MonoBehaviour
{
    IEnumerator Start()
    {
        IEnumerator walkthrough = Walkthrough();
        while (true)
        {
            bool hasNext;
            object current = null;
            try
            {
                hasNext = walkthrough.MoveNext();
                if (hasNext) current = walkthrough.Current;
            }
            catch (Exception exception)
            {
                Debug.LogError("[MercatoSeamlessPlayModeVerifier] FAIL: " + exception.Message + "\n" + exception.StackTrace);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (!hasNext) break;
            yield return current;
        }

        Debug.Log("[MercatoSeamlessPlayModeVerifier] PASS: 5 exterior -> inn -> exterior crossings blended toward the authored room profile and back to the live exterior profile; one camera override owner, one registry module, active local lighting, protected battle lock, and no duplicate runtime authorities.");
        UnityEditor.EditorApplication.isPlaying = false;
    }

    IEnumerator Walkthrough()
    {
        yield return null;
        yield return new WaitForFixedUpdate();

        Require(SceneManager.GetActiveScene().name == "MercatoVecchio", "active scene changed before the seamless walkthrough");
        GameObject player = GameObject.FindWithTag("Player");
        Require(player != null, "Player-tagged exploration actor is missing");
        Rigidbody body = player.GetComponent<Rigidbody>();
        Require(body != null, "Player Rigidbody is missing");

        SeamlessInteriorModule module = FindAnyObjectByType<SeamlessInteriorModule>();
        Require(module != null && module.SubLocationId == "albergo_fiorentino_floor1", "Florentine Inn module is missing or misidentified");
        Require(module.TryValidateRuntime(out string moduleError), "module contract is invalid: " + moduleError);
        SeamlessInteriorPortal portal = module.Portal;
        Require(portal != null, "inn threshold portal is missing");
        Require(FindObjectsByType<SeamlessInteriorModule>(FindObjectsInactive.Exclude).Length == 1, "duplicate seamless interior modules loaded");
        Require(FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude).Length == 1, "duplicate players loaded");
        Require(FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Count(camera => camera.CompareTag("MainCamera")) == 1, "duplicate Main Cameras loaded");
        DynamicZoom zoom = FindAnyObjectByType<DynamicZoom>();
        Require(zoom != null, "shared DynamicZoom camera adapter is missing");
        Require(zoom.ActiveOverrideCount == 0, "camera began with a residual interior override");

        var legacySave = new SaveData
        {
            saveVersion = 6,
            sceneName = "FlorentineInnFloor1",
            playerX = 1.5f,
            playerY = 0f,
            playerZ = -2f
        };
        Require(SaveSystem.MigrateLegacyLocation(legacySave), "legacy standalone-inn save was not migrated");
        Require(legacySave.sceneName == "MercatoVecchio" &&
                legacySave.subLocationId == "albergo_fiorentino_floor1" &&
                legacySave.migratedLegacyInnScenePosition,
            "legacy inn save migration did not preserve the owning-zone/sub-location contract");

        Vector3 outside = portal.transform.TransformPoint(new Vector3(0f, -1.15f, -1.2f));
        Vector3 inside = portal.transform.TransformPoint(new Vector3(0f, -1.15f, 1.2f));
        body.position = outside;
        Physics.SyncTransforms();
        yield return new WaitForSecondsRealtime(0.3f);
        Vector3 exteriorOffset = zoom.AppliedOffset;
        // Mirrors the inn builder's authored follow + composition offsets.
        Vector3 authoredInteriorOffset = new Vector3(1.45f, 8.95f, -10.9f);

        for (int repetition = 0; repetition < 5; repetition++)
        {
            yield return MoveAcross(body, outside, inside);
            Require(module.PlayerInside, $"crossing {repetition + 1}: module did not enter interior state");
            Require(SeamlessInteriorRegistry.ActiveSubLocationId == "albergo_fiorentino_floor1", $"crossing {repetition + 1}: registry did not activate the inn");
            Require(module.GetComponentsInChildren<Light>(true).Where(light => light.type != LightType.Directional).Any(light => light.intensity > 0f),
                $"crossing {repetition + 1}: local interior lights did not activate");
            Require(zoom.ActiveOverrideCount == 1,
                $"crossing {repetition + 1}: interior camera override was not owned exactly once");
            Vector3 entryOffset = zoom.AppliedOffset;
            yield return new WaitForSecondsRealtime(0.4f);
            Require(Vector3.Distance(zoom.AppliedOffset, authoredInteriorOffset) + 0.03f <
                    Vector3.Distance(entryOffset, authoredInteriorOffset),
                $"crossing {repetition + 1}: camera did not blend toward the authored inn profile");

            module.SetBattleLocked(true);
            Require(module.BattleLocked && portal.BattleLocked, "protected battle lock did not close the inn portal");
            module.SetBattleLocked(false);
            Require(!module.BattleLocked && !portal.BattleLocked, "protected battle lock did not release the inn portal");

            yield return MoveAcross(body, inside, outside);
            Require(!module.PlayerInside, $"crossing {repetition + 1}: module did not restore exterior state");
            Require(string.IsNullOrEmpty(SeamlessInteriorRegistry.ActiveSubLocationId), $"crossing {repetition + 1}: registry did not clear the inn");
            Require(module.GetComponentsInChildren<Light>(true).Where(light => light.type != LightType.Directional).All(light => light.intensity <= 0.001f),
                $"crossing {repetition + 1}: local interior lights remained active outside");
            Require(zoom.ActiveOverrideCount == 0,
                $"crossing {repetition + 1}: camera override remained after leaving the inn");
            Vector3 exitOffset = zoom.AppliedOffset;
            yield return new WaitForSecondsRealtime(0.45f);
            Require(Vector3.Distance(zoom.AppliedOffset, exteriorOffset) + 0.03f <
                    Vector3.Distance(exitOffset, exteriorOffset),
                $"crossing {repetition + 1}: camera did not blend back toward the live exterior profile");
        }

        Require(!SeamlessInteriorRegistry.TryRestore("missing_sub_location", player.transform, out _, out string missingError) &&
                !string.IsNullOrEmpty(missingError),
            "missing sub-location restore did not fail safely");
        Require(FindObjectsByType<SeamlessInteriorModule>(FindObjectsInactive.Exclude).Length == 1, "repeated crossings duplicated the module");
    }

    static IEnumerator MoveAcross(Rigidbody body, Vector3 from, Vector3 to)
    {
        body.position = from;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        const int steps = 12;
        for (int step = 1; step <= steps; step++)
        {
            body.position = Vector3.Lerp(from, to, step / (float)steps);
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
