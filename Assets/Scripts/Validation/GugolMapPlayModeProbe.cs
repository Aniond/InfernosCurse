#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed class GugolMapPlayModeProbe : MonoBehaviour
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
                Debug.LogError("[GugolMapPlayModeVerifier] FAIL: " + exception.Message + "\n" + exception.StackTrace);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (!hasNext) break;
            yield return current;
        }

        Debug.Log("[GugolMapPlayModeVerifier] PASS: phone-width bounded current-location card without rating stats -> exact street search -> Mercato Street View -> 3 venues + 5 known NPC markers -> venue card -> Tuscany/Italy navigation -> close/reopen without duplicate canvases.");
        UnityEditor.EditorApplication.isPlaying = false;
    }

    IEnumerator Walkthrough()
    {
        yield return null;
        var map = FindAnyObjectByType<GugolMapUI>();
        Require(map != null, "GugolMapUI runtime authority is missing");
        Require(map.Open(), "map did not open from Mercato");
        yield return new WaitForSecondsRealtime(0.1f);

        Require(GugolMapUI.IsOpen && map.CurrentView == GugolMapViewKind.City,
            "map did not open on the City layer");
        Require(!map.ValidationHasWordmark, "retired Gugol Mappe wordmark exists in the browsing hierarchy");
        Require(FindObjectsByType<Canvas>().Count(canvas => canvas.name == "GugolMapCanvas") == 1,
            "map opened with a duplicate canvas");

        int originalWidth = Screen.width;
        int originalHeight = Screen.height;
        Screen.SetResolution(390, 844, false);
        yield return new WaitForSecondsRealtime(0.25f);
        Require(map.ValidationShowCurrentLocationCard(), "current-location card did not open");
        yield return null;
        var directionsCard = FindAnyObjectByType<GugolDirectionsCard>();
        Require(directionsCard != null && directionsCard.ValidationHasScrollViewport,
            "current-location card has no bounded scroll viewport");
        Require(!directionsCard.ValidationStatsVisible,
            "current-location card still exposes destination rating/review stats");
        RectTransform panelRect = directionsCard.ValidationPanelRect;
        RectTransform canvasRect = panelRect != null ? panelRect.GetComponentInParent<Canvas>().transform as RectTransform : null;
        Require(panelRect != null && canvasRect != null &&
                panelRect.rect.width <= canvasRect.rect.width && panelRect.rect.height <= canvasRect.rect.height,
            "current-location card exceeds the active canvas bounds");
        Screen.SetResolution(originalWidth, originalHeight, false);
        yield return new WaitForSecondsRealtime(0.2f);

        var exact = map.ValidationSearchFirst("Mercato Vecchio");
        Require(exact != null && exact.kind == GugolMapFeatureKind.Street &&
                exact.featureId == "mercato_vecchio_square",
            "exact street search did not prioritize Mercato Vecchio");

        Require(map.ValidationOpenStreet("mercato_vecchio_square"), "Mercato Street View did not open");
        yield return new WaitForSecondsRealtime(0.4f);
        Require(map.CurrentView == GugolMapViewKind.Street &&
                map.FocusedStreetId == "mercato_vecchio_square",
            "Street View focus was not retained");
        Require(map.ValidationFeatureVisualCount == 8,
            $"Mercato Street View expected 8 feature markers, got {map.ValidationFeatureVisualCount}");

        Require(map.ValidationOpenFeature(GugolMapFeatureKind.Venue, "albergo_fiorentino"),
            "Florentine Inn map feature is unavailable");
        Require(map.ValidationContextCardOpen, "Florentine Inn did not open its context card");

        map.ValidationExitStreet();
        yield return new WaitForSecondsRealtime(0.4f);
        Require(map.CurrentView == GugolMapViewKind.City, "Street back-navigation did not restore City");
        map.ValidationSetLayer(MapLevel.Region);
        Require(map.CurrentView == GugolMapViewKind.Region, "Tuscany layer did not activate");
        map.ValidationSetLayer(MapLevel.World);
        Require(map.CurrentView == GugolMapViewKind.World, "Italy layer did not activate");
        map.ValidationSetLayer(MapLevel.City);

        map.Close();
        Require(!GugolMapUI.IsOpen && Mathf.Approximately(Time.timeScale, 1f),
            "closing the map did not restore play time");
        Require(map.Open(), "map did not reopen");
        Require(FindObjectsByType<Canvas>().Count(canvas => canvas.name == "GugolMapCanvas") == 1,
            "reopening the map duplicated its canvas");
        map.Close();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
