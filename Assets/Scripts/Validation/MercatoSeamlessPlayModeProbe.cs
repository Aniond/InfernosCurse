#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

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
        CinemachineCamera sharedCamera = zoom.GetComponent<CinemachineCamera>();
        Require(sharedCamera != null, "shared Cinemachine camera is missing");
        Camera mainCamera = Camera.main;
        Require(mainCamera != null, "Main Camera is missing");
        CameraOcclusionFader fader = mainCamera.GetComponent<CameraOcclusionFader>();
        Require(fader != null, "shared camera occlusion fader is missing");
        Transform facade = FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .FirstOrDefault(transform => transform.name == "AlbergoFiorentino_Facade");
        Require(facade != null, "Albergo Fiorentino facade is missing");
        Renderer[] facadeRenderers = facade.GetComponentsInChildren<Renderer>(true);
        Require(facadeRenderers.Length > 0, "Albergo Fiorentino facade has no renderers");
        Renderer[] roofRenderers = module.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.name.StartsWith("InnRoof_", StringComparison.Ordinal))
            .ToArray();
        Require(roofRenderers.Length == 2, "Albergo Fiorentino requires two authored roof occluders");
        Require(roofRenderers.All(renderer => renderer.GetComponent<Collider>() != null),
            "Albergo Fiorentino roof occluders require colliders for camera sightline detection");
        Renderer[] cameraFacingWalls = module.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.name.StartsWith("InnWall_", StringComparison.Ordinal) &&
                                module.transform.InverseTransformPoint(transform.position).x < -0.5f)
            .SelectMany(transform => transform.GetComponentsInChildren<Renderer>(true))
            .Distinct()
            .ToArray();
        Require(cameraFacingWalls.Length > 1, "Albergo Fiorentino camera-facing wall set is missing");

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
        Vector3 deepInside = portal.transform.TransformPoint(new Vector3(0f, -1.15f, 8f));
        body.position = outside;
        Physics.SyncTransforms();
        yield return new WaitForSecondsRealtime(0.3f);
        Vector3 exteriorOffset = zoom.AppliedOffset;
        Vector3 authoredInteriorOffset = new Vector3(0f, 8.2f, -9.2f);

        for (int repetition = 0; repetition < 5; repetition++)
        {
            yield return MoveAcross(body, outside, inside);
            yield return MoveAcross(body, inside, deepInside);
            Require(module.PlayerInside, $"crossing {repetition + 1}: module did not enter interior state");
            Require(SeamlessInteriorRegistry.ActiveSubLocationId == "albergo_fiorentino_floor1", $"crossing {repetition + 1}: registry did not activate the inn");
            Require(module.GetComponentsInChildren<Light>(true).Where(light => light.type != LightType.Directional).Any(light => light.intensity > 0f),
                $"crossing {repetition + 1}: local interior lights did not activate");
            Require(zoom.ActiveOverrideCount == 1,
                $"crossing {repetition + 1}: interior camera override was not owned exactly once");
            Require(zoom.target == player.transform && sharedCamera.Follow == player.transform,
                $"crossing {repetition + 1}: shared camera stopped following the player");
            Require(facadeRenderers.All(fader.IsApprovedOccluder),
                $"crossing {repetition + 1}: facade coverage was not applied to the interior occlusion profile");
            Vector3 entryOffset = zoom.AppliedOffset;
            yield return new WaitForSecondsRealtime(0.4f);
            Require(Vector3.Distance(zoom.AppliedOffset, authoredInteriorOffset) + 0.03f <
                    Vector3.Distance(entryOffset, authoredInteriorOffset),
                $"crossing {repetition + 1}: camera did not blend toward the authored inn profile");
            Vector3 viewport = mainCamera.WorldToViewportPoint(player.transform.position + Vector3.up * 0.9f);
            Require(viewport.z > 0f && viewport.x >= 0.08f && viewport.x <= 0.92f &&
                    viewport.y >= 0.08f && viewport.y <= 0.92f,
                $"crossing {repetition + 1}: player left the safe camera frame at viewport {viewport}");

            Vector3 sightlineTarget = player.transform.position + Vector3.up * fader.targetHeight;
            Vector3 sightline = sightlineTarget - mainCamera.transform.position;
            RaycastHit[] sightlineHits = Physics.SphereCastAll(
                mainCamera.transform.position,
                fader.probeRadius,
                sightline.normalized,
                sightline.magnitude,
                ~0,
                QueryTriggerInteraction.Ignore);
            Renderer[] innSightlineBlockers = sightlineHits
                .Where(hit => hit.collider != null &&
                              (hit.collider.transform.IsChildOf(module.transform) || hit.collider.transform.IsChildOf(facade)))
                .Select(hit => hit.collider.GetComponent<Renderer>() ?? hit.collider.GetComponentInChildren<Renderer>())
                .Where(renderer => renderer != null && fader.IsApprovedOccluder(renderer))
                .Distinct()
                .ToArray();
            Require(innSightlineBlockers.Any(renderer => roofRenderers.Contains(renderer)),
                $"crossing {repetition + 1}: west-entry camera sightline did not exercise an inn roof occluder");
            Require(innSightlineBlockers.All(fader.IsRendererHidden),
                $"crossing {repetition + 1}: inn roof or wall remained opaque across the west-entry camera sightline");
            Require(roofRenderers.All(fader.IsRendererHidden),
                $"crossing {repetition + 1}: the inn roof did not open into a complete dollhouse cutaway");
            Require(cameraFacingWalls.All(fader.IsRendererHidden),
                $"crossing {repetition + 1}: a camera-facing inn wall remained opaque inside the dollhouse cutaway");
            Require(facadeRenderers.All(fader.IsRendererHidden),
                $"crossing {repetition + 1}: the exterior facade remained opaque inside the dollhouse cutaway");
            foreach (RaycastHit hit in sightlineHits.Where(hit => hit.collider != null && hit.collider.transform.IsChildOf(facade)))
            {
                Renderer blocker = hit.collider.GetComponent<Renderer>() ?? hit.collider.GetComponentInChildren<Renderer>();
                Require(blocker == null || fader.IsRendererHidden(blocker),
                    $"crossing {repetition + 1}: facade renderer {blocker?.name} remained opaque across the player sightline");
            }
            module.SetBattleLocked(true);
            Require(module.BattleLocked && portal.BattleLocked, "protected battle lock did not close the inn portal");
            module.SetBattleLocked(false);
            Require(!module.BattleLocked && !portal.BattleLocked, "protected battle lock did not release the inn portal");

            yield return MoveAcross(body, deepInside, outside);
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
