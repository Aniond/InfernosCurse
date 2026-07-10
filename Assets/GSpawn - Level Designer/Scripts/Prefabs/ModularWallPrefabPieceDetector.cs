#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GSPAWN
{
    public enum WallCornerMode
    {
        Unknown = 0,
        LCorner,
        Segment
    }

    public enum WallPillarMode
    {
        None = 0,
        Middle,   // 2 pillars (CornerMiddle)
        Edge      // 4 pillars (CornerBegin/End)
    }

    public struct WallConfig
    {
        public WallCornerMode cornerMode;
        public WallPillarMode pillarMode;

        public bool isValid
        {
            get { return cornerMode != WallCornerMode.Unknown; }
        }
    }

    public static class ModularWallPrefabPieceDetector
    {
        class WallPiece
        {
            public GameObject   gameObject;
            public OBB          obb = OBB.getInvalid();
            public PluginPrefab pluginPrefab;
        }

        public static bool detectWallPieces(GameObject parent)
        {
            var wallPieces = new List<WallPiece>();
            if (!extractChildWallPieces(parent, wallPieces))
                return false;

            OBB hierarchyOBB;
            if (!tryGetHierarchyOBB(parent, out hierarchyOBB))
                return false;

            // Detect wall config type
            var config = detectConfigType(wallPieces);
            if (!config.isValid)
                return false;

            if (!validatePieceCount(wallPieces, config))
                return false;

            // Detect based on config
            bool result = false;
            switch (config.cornerMode)
            {
                case WallCornerMode.LCorner:

                    result = detect_LCorner(parent, hierarchyOBB, wallPieces, config);
                    break;

                case WallCornerMode.Segment:

                    result = detect_Segment(parent, hierarchyOBB, wallPieces, config);
                    break;

                default:

                    return false;
            }

            if (!result)
                return false;

            if (!validateUniqueWallPieces(wallPieces))
                return false;

            AssetDatabase.SaveAssetIfDirty(parent);
            return true;
        }

        static WallConfig detectConfigType(List<WallPiece> pieces)
        {
            WallConfig config = new WallConfig();
            config.cornerMode = WallCornerMode.Unknown;
            config.pillarMode = WallPillarMode.None;

            bool hasInnerCorner = false;
            int pillarCount = 0;

            foreach (var piece in pieces)
            {
                var tag = piece.pluginPrefab.tag;

                if (tag == WallPrefabTag.WallInnerCorner)
                    hasInnerCorner = true;

                if (tag == WallPrefabTag.WallPillar)
                    pillarCount++;
            }

            // Corner mode
            if (hasInnerCorner)
                config.cornerMode = WallCornerMode.LCorner;
            else
                config.cornerMode = WallCornerMode.Segment;

            // Pillar mode
            if (pillarCount == 0)
            {
                config.pillarMode = WallPillarMode.None;
            }
            else if (pillarCount == 2)
            {
                config.pillarMode = WallPillarMode.Middle;
            }
            else if (pillarCount == 4)
            {
                config.pillarMode = WallPillarMode.Edge;
            }
            else
            {
                Debug.LogError("Invalid number of pillar pieces: " + pillarCount + ". Expected 0, 2, or 4.");
                config.cornerMode = WallCornerMode.Unknown;
                return config;
            }

            return config;
        }

        static int getExpectedPieceCount(WallConfig config)
        {
            switch (config.cornerMode)
            {
                case WallCornerMode.LCorner:
                {
                    switch (config.pillarMode)
                    {
                        case WallPillarMode.None:   return 5;
                        case WallPillarMode.Middle: return 7;
                        case WallPillarMode.Edge:   return 9;
                    }
                    break;
                }

                case WallCornerMode.Segment:
                {
                    switch (config.pillarMode)
                    {
                        case WallPillarMode.None:   return 7;
                        case WallPillarMode.Middle: return 9;
                        case WallPillarMode.Edge:   return -1; // Invalid
                    }
                    break;
                }
            }

            return -1;
        }

        static bool validatePieceCount(List<WallPiece> pieces, WallConfig config)
        {
            int expected = getExpectedPieceCount(config);
            if (expected < 0)
            {
                Debug.LogError("Invalid wall configuration.");
                return false;
            }

            if (pieces.Count != expected)
            {
                Debug.LogError(
                    $"Invalid wall structure. Expected {expected} pieces for {config.cornerMode} + {config.pillarMode}, but found {pieces.Count}."
                );
                return false;
            }

            return true;
        }

        static bool detect_LCorner(GameObject parent, OBB hierarchyOBB, List<WallPiece> wallPieces, WallConfig config)
        {
            if (!resolveMidSegment(wallPieces, hierarchyOBB, out WallPiece midSegment))
                return false;

            if (!resolveInnerCorner(wallPieces, midSegment, out WallPiece innerCorner))
                return false;

            if (!resolveOuterCorner(wallPieces, midSegment, innerCorner, out WallPiece outerCorner))
                return false;

            if (!resolveEndSegmentsForLCorner(wallPieces, midSegment, innerCorner, outerCorner, out WallPiece first, out WallPiece last))
                return false;

            // Pillars
            switch (config.pillarMode)
            {
                case WallPillarMode.None:

                    break;

                case WallPillarMode.Middle:

                    if (!resolveCornerMiddlePillars(wallPieces, midSegment, first, last, out _, out _))
                        return false;
                    break;

                case WallPillarMode.Edge:

                    if (!resolveCornerEdgePillars(wallPieces, first, out _, out _, out _, out _))
                        return false;
                    break;
            }

            return true;
        }

        static bool detect_Segment(GameObject parent, OBB hierarchyOBB, List<WallPiece> wallPieces, WallConfig config)
        {
            if (!resolveMidSegment(wallPieces, hierarchyOBB, out WallPiece midSegment))
                return false;

            if (!resolveEndSegments(wallPieces, midSegment, out WallPiece first, out WallPiece last))
                return false;

            switch (config.pillarMode)
            {
                case WallPillarMode.None:

                    break;

                case WallPillarMode.Middle:

                    if (!resolveCornerMiddlePillars(wallPieces, midSegment, first, last, out _, out _))
                        return false;
                    break;

                case WallPillarMode.Edge:

                    Debug.LogError("Edge pillars not supported in Segment mode.");
                    return false;
            }

            return true;
        }

        static bool extractChildWallPieces(GameObject parent, List<WallPiece> wallPieces)
        {
            wallPieces.Clear();

            OBB obb;
            int childCount = parent.transform.childCount;
            for (int i = 0; i < childCount; ++i)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                if (!tryGetHierarchyOBB(child, out obb))
                    return false;

                GameObject prefabAsset = child.getOutermostPrefabAsset();
                if (prefabAsset == null)
                {
                    Debug.LogError("One of the child wall pieces is not a prefab instance: " + child.name + ".");
                    return false;
                }
                PluginPrefab pluginPrefab = PrefabLibProfileDb.instance.getPrefab(prefabAsset);
                if (pluginPrefab == null)
                {
                    Debug.LogError("One of the child wall pieces is not an instance of a valid plugin prefab: " + child.name + ".");
                    return false;
                }

                WallPiece wallPiece     = new WallPiece();
                wallPiece.gameObject    = child;
                wallPiece.obb           = obb;
                wallPiece.pluginPrefab  = pluginPrefab;
                wallPieces.Add(wallPiece);
            }

            return true;
        }

        static bool tryGetHierarchyOBB(GameObject parent, out OBB obb)
        {
            obb = ObjectBounds.calcHierarchyWorldOBB(parent, ModularWallPrefabProfile.getWallBoundsQConfig());

            if (!obb.isValid)
            {
                Debug.LogError("Failed to calculate bounding volume for wall piece: " + parent.name + ".");
                return false;
            }

            return true;
        }

        static bool resolveMidSegment(List<WallPiece> pieces, OBB hierarchyOBB, out WallPiece midSegment)
        {
            midSegment = null;

            midSegment = findMiddleSegment(pieces, hierarchyOBB);
            if (midSegment == null)
            {
                Debug.LogError("Missing MiddleSegment piece.");
                return false;
            }

            midSegment.gameObject.name = ModularWallPrefabProfile.middleSegmentName;
            return true;
        }

        static bool resolveEndSegmentsForLCorner(List<WallPiece> wallPieces, WallPiece midSegment, WallPiece innerCorner, WallPiece outerCorner, out WallPiece first, out WallPiece last)
        {
            first = null;
            last  = null;

            float dMinO = float.MaxValue;
            float dMinI = float.MaxValue;

            foreach (var piece in wallPieces)
            {
                // Ignore piece we already know about
                if (piece == innerCorner || piece == outerCorner || piece == midSegment)
                    continue;

                // No tags
                if (piece.pluginPrefab.tag != WallPrefabTag.None)
                    continue;

                // Calculate distance between OBB centers
                float dOuter = (outerCorner.obb.center - piece.obb.center).magnitude;
                float dInner = (innerCorner.obb.center - piece.obb.center).magnitude;

                // Closest to outer corner -> first
                if (dOuter < dMinO)
                {
                    dMinO = dOuter;
                    first = piece;
                }

                // Closest to inner corner -> last
                if (dInner < dMinI)
                {
                    dMinI = dInner;
                    last  = piece;
                }
            }

            if (first == null)
            {
                Debug.LogError("Failed to resolve FirstSegment for LCorner.");
                return false;
            }

            if (last == null)
            {
                Debug.LogError("Failed to resolve LastSegment for LCorner.");
                return false;
            }

            if (first == last)
            {
                Debug.LogError("Invalid wall structure: FirstSegment same as LastSegment.");
                return false;
            }

            first.gameObject.name = ModularWallPrefabProfile.firstSegmentName;
            last.gameObject.name  = ModularWallPrefabProfile.lastSegmentName;

            return true;
        }

        static bool resolveEndSegments(List<WallPiece> pieces, WallPiece midSegment, out WallPiece first, out WallPiece last)
        {
            first = null;
            last  = null;
            (first, last) = findEndSegments(pieces, midSegment);

            if (first == null)
            {
                Debug.LogError("FirstSegment wall piece is missing.");
                return false;
            }

            if (last == null)
            {
                Debug.LogError("LastSegment wall piece is missing.");
                return false;
            }

            if (first == last)
            {
                Debug.LogError("Invalid wall structure: FirstSegment same as LastSegment.");
                return false;
            }

            // Check existing names
            string firstName = first.gameObject.name.ToLower();
            string lastName  = last.gameObject.name.ToLower();

            bool firstIsFirst = firstName == ModularWallPrefabProfile.firstSegmentName_LC;
            bool firstIsLast  = firstName == ModularWallPrefabProfile.lastSegmentName_LC;

            bool lastIsFirst  = lastName == ModularWallPrefabProfile.firstSegmentName_LC;
            bool lastIsLast   = lastName == ModularWallPrefabProfile.lastSegmentName_LC;

            // Already correct
            if (!(firstIsFirst && lastIsLast))
            {
                // Swapped
                if (firstIsLast && lastIsFirst)
                {
                    var tmp = first;
                    first   = last;
                    last    = tmp;
                }
                // Only one labeled; enforce
                else if (firstIsLast || lastIsFirst)
                {
                    var tmp = first;
                    first   = last;
                    last    = tmp;
                }
            }

            // Final naming (authoritative)
            first.gameObject.name = ModularWallPrefabProfile.firstSegmentName;
            last.gameObject.name  = ModularWallPrefabProfile.lastSegmentName;
            return true;
        }

        static bool resolveCornerMiddlePillars(List<WallPiece> pieces, WallPiece midSegment, WallPiece first, WallPiece last, out WallPiece outerPillar, out WallPiece innerPillar)
        {
            outerPillar = null;
            innerPillar = null;

            // Collect pillar candidates
            List<WallPiece> pillarCandidates = new List<WallPiece>();
            foreach (var piece in pieces)
            {
                if (piece == midSegment || piece == first || piece == last)
                    continue;

                if (piece.pluginPrefab.tag != WallPrefabTag.WallPillar)
                    continue;

                pillarCandidates.Add(piece);
            }

            // Detect based on proximity
            float bestOuterDist = float.MaxValue;
            float bestInnerDist = float.MaxValue;
            foreach (var pillar in pillarCandidates)
            {
                float distToFirst = (pillar.obb.center - first.obb.center).sqrMagnitude;
                float distToLast  = (pillar.obb.center - last.obb.center).sqrMagnitude;

                if (distToFirst < bestOuterDist)
                {
                    bestOuterDist = distToFirst;
                    outerPillar   = pillar;
                }

                if (distToLast < bestInnerDist)
                {
                    bestInnerDist = distToLast;
                    innerPillar   = pillar;
                }
            }

            if (outerPillar == null)
            {
                Debug.LogError("Missing PillarOuterCornerMiddle.");
                return false;
            }

            if (innerPillar == null)
            {
                Debug.LogError("Missing PillarInnerCornerMiddle.");
                return false;
            }

            if (outerPillar == innerPillar)
            {
                Debug.LogError("Invalid pillar setup: Outer and Inner Middle Corner pillars resolved to same object.");
                return false;
            }

            outerPillar.gameObject.name = ModularWallPrefabProfile.pillarOuterCornerMiddleName;
            innerPillar.gameObject.name = ModularWallPrefabProfile.pillarInnerCornerMiddleName;

            return true;
        }

        static bool resolveCornerEdgePillars(List<WallPiece> pieces, WallPiece first, 
            out WallPiece outerBegin, out WallPiece outerEnd, out WallPiece innerBegin, out WallPiece innerEnd)
        {
            outerBegin = null;
            outerEnd   = null;
            innerBegin = null;
            innerEnd   = null;

            // Collect pillar candidates
            List<WallPiece> candidates = new List<WallPiece>();
            foreach (var piece in pieces)
            {
                if (piece.pluginPrefab.tag == WallPrefabTag.WallPillar)
                    candidates.Add(piece);
            }

            if (candidates.Count != 4)
            {
                Debug.LogError("Invalid number of edge pillars. Expected 4, got " + candidates.Count + ".");
                return false;
            }

            // Step 1: Closest to first -> outerBegin
            float dMin = float.MaxValue;
            foreach (var p in candidates)
            {
                float d = (p.obb.center - first.obb.center).sqrMagnitude;
                if (d < dMin)
                {
                    dMin = d;
                    outerBegin = p;
                }
            }
            candidates.Remove(outerBegin);

            // Step 2: Closest to outerBegin -> outerEnd
            dMin = float.MaxValue;
            foreach (var p in candidates)
            {
                float d = (p.obb.center - outerBegin.obb.center).sqrMagnitude;
                if (d < dMin)
                {
                    dMin = d;
                    outerEnd = p;
                }
            }
            candidates.Remove(outerEnd);

            // Step 3: Closest to outerEnd -> innerBegin
            dMin = float.MaxValue;
            foreach (var p in candidates)
            {
                float d = (p.obb.center - outerEnd.obb.center).sqrMagnitude;
                if (d < dMin)
                {
                    dMin = d;
                    innerBegin = p;
                }
            }
            candidates.Remove(innerBegin);

            // Step 4: remaining -> innerEnd
            innerEnd = candidates[0];

            // Validation
            if (outerBegin == null || outerEnd == null || innerBegin == null || innerEnd == null)
            {
                Debug.LogError("Failed to resolve edge pillars.");
                return false;
            }

            // Naming
            outerBegin.gameObject.name = ModularWallPrefabProfile.pillarOuterCornerBeginName;
            outerEnd.gameObject.name   = ModularWallPrefabProfile.pillarOuterCornerEndName;
            innerBegin.gameObject.name = ModularWallPrefabProfile.pillarInnerCornerBeginName;
            innerEnd.gameObject.name   = ModularWallPrefabProfile.pillarInnerCornerEndName;

            return true;
        }

        static bool resolveInnerCorner(List<WallPiece> pieces, WallPiece midSegment, out WallPiece innerCorner)
        {
            innerCorner = null;

            // Find inner corner by tag
            foreach (var piece in pieces)
            {
                if (piece.pluginPrefab.tag == WallPrefabTag.WallInnerCorner)
                {
                    innerCorner = piece;
                    break;
                }
            }

            if (innerCorner == null)
            {
                Debug.LogError("Missing Inner Corner piece. Please make sure all required wall pieces exist.");
                return false;
            }

            innerCorner.gameObject.name = ModularWallPrefabProfile.innerCornerName;
            return true;
        }

        static bool resolveOuterCorner(List<WallPiece> pieces, WallPiece midSegment, WallPiece innerCorner, out WallPiece outerCorner)
        {
            outerCorner = null;

            float dMin = float.MaxValue;
            foreach (var piece in pieces)
            {
                // Ignore known pieces
                if (piece == innerCorner || piece == midSegment)
                    continue;

                // No tags
                if (piece.pluginPrefab.tag != WallPrefabTag.None)
                    continue;

                // Distance to mid segment
                float d = (midSegment.obb.center - piece.obb.center).sqrMagnitude;
                if (d < dMin)
                {
                    dMin = d;
                    outerCorner = piece;
                }
            }

            if (outerCorner == null)
            {
                Debug.LogError("Missing Outer Corner piece.");
                return false;
            }

            outerCorner.gameObject.name = ModularWallPrefabProfile.outerCornerName;
            return true;
        }

        static WallPiece findMiddleSegment(List<WallPiece> pieces, OBB hierarchyOBB)
        {
            if (pieces.Count == 0)
                return null;

            // Compute average center
            Vector3 avg = Vector3.zero;
            foreach (var piece in pieces)
                avg += piece.obb.center;

            avg /= pieces.Count;

            // Find closest piece to average
            float dMin = float.MaxValue;
            WallPiece result = null;
            foreach (var piece in pieces)
            {
                if (piece.pluginPrefab.tag != WallPrefabTag.None)
                    continue;

                float d = (piece.obb.center - avg).sqrMagnitude;
                if (d < dMin)
                {
                    dMin = d;
                    result = piece;
                }
            }

            return result;
        }

        static (WallPiece first, WallPiece last) findEndSegments(List<WallPiece> pieces, WallPiece midSegment)
        {
            float maxDist0 = float.MinValue;
            float maxDist1 = float.MinValue;

            WallPiece first = null;
            WallPiece last  = null;

            foreach (var piece in pieces)
            {
                if (piece == midSegment)
                    continue;

                if (piece.pluginPrefab.tag != WallPrefabTag.None)
                    continue;

                float dist = (piece.obb.center - midSegment.obb.center).sqrMagnitude;
                if (dist > maxDist0)
                {
                    maxDist1 = maxDist0;
                    last     = first;

                    maxDist0 = dist;
                    first    = piece;
                }
                else if (dist > maxDist1)
                {
                    maxDist1 = dist;
                    last     = piece;
                }
            }

            return (first, last);
        }

        static bool validateUniqueWallPieces(List<WallPiece> pieces)
        {
            int count = pieces.Count;
            if (count == 0) 
                return false;

            for (int i = 0; i < count; ++i)
            {
                WallPiece a = pieces[i];
                for (int j = i + 1; j < count; ++j)
                {
                    WallPiece b = pieces[j];
                    if (a.gameObject == b.gameObject)
                    {
                        Debug.LogError(
                            "Two wall pieces are referencing the same object: " + a.gameObject.name + ". " +
                            "Please check wall piece placement and remove ambiguities.");
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
#endif