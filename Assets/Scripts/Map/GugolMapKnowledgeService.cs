using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds player-visible map state from authored discoveries, warnings,
/// outcomes, weather, and explicit NPC knowledge. Hidden Circle and player
/// Sanity state are intentionally outside this service.
/// </summary>
public static class GugolMapKnowledgeService
{
    public static GugolMapKnowledgeSnapshot Build(HubMap hub)
    {
        ExplorationDiscoveryLedger.LoadResourceDefinitions();
        var streets = Resources.LoadAll<GugolStreetDefinition>("GugolMap/Streets");
        var venues = Resources.LoadAll<GugolVenueDefinition>("GugolMap/Venues");
        var npcs = Resources.LoadAll<GugolNpcMapDefinition>("GugolMap/Npcs");
        var expressions = Resources.LoadAll<GugolMapWorldExpressionDefinition>("GugolMap/WorldExpressions");
        var features = new List<GugolMapFeatureRecord>();

        var expressionBySite = BuildExpressionsBySite(expressions);
        var activeOutcomes = SiteOutcomeState.Export();
        var activeWarnings = CircleWarningLedger.Export();
        string dayKey = CurrentDayKey();

        if (hub != null)
        {
            hub.EnsureGraphBuilt();
            foreach (HubNode node in hub.AllNodes)
            {
                if (node == null || node.kind == NodeKind.Waypoint) continue;
                var state = StateForDiscovery(node.discoveryId);
                if (!MapRouting.IsVisible(node) && state >= GugolMapKnowledgeState.Known)
                    state = GugolMapKnowledgeState.Hidden;
                var feature = new GugolMapFeatureRecord
                {
                    featureId = node.id,
                    kind = GugolMapFeatureKind.Location,
                    knowledgeState = state,
                    displayName = state == GugolMapKnowledgeState.Rumored ? node.blurb : node.displayName,
                    subtitle = node.blurb,
                    searchText = node.displayName + " " + node.blurb,
                    nodeId = node.id,
                    siteId = node.id,
                    node = node,
                };
                ApplyWorldExpression(feature, expressionBySite, activeOutcomes, activeWarnings);
                features.Add(feature);
            }
        }

        foreach (var street in streets)
        {
            if (street == null) continue;
            var state = StateForDiscovery(street.discoveryId);
            string visibleName = state == GugolMapKnowledgeState.Rumored && !string.IsNullOrWhiteSpace(street.rumorLabel)
                ? street.rumorLabel
                : street.displayName;
            var feature = new GugolMapFeatureRecord
            {
                featureId = street.streetId,
                kind = GugolMapFeatureKind.Street,
                knowledgeState = state,
                displayName = visibleName,
                subtitle = state == GugolMapKnowledgeState.Rumored ? street.rumorLabel : street.displayName,
                searchText = visibleName,
                streetId = street.streetId,
                street = street,
            };
            ApplyWorldExpression(feature, expressionBySite, activeOutcomes, activeWarnings);
            features.Add(feature);
        }

        foreach (var venue in venues)
        {
            if (venue == null) continue;
            var state = StateForDiscovery(venue.discoveryId);
            string visibleName = state == GugolMapKnowledgeState.Rumored && !string.IsNullOrWhiteSpace(venue.rumorLabel)
                ? venue.rumorLabel
                : venue.displayName;
            var feature = new GugolMapFeatureRecord
            {
                featureId = venue.venueId,
                kind = GugolMapFeatureKind.Venue,
                knowledgeState = state,
                displayName = visibleName,
                subtitle = BuildVenueSubtitle(venue),
                searchText = visibleName + " " + string.Join(" ", venue.services ?? Array.Empty<string>()),
                streetId = venue.streetId,
                venueId = venue.venueId,
                siteId = venue.siteId,
                venue = venue,
            };
            ApplyWorldExpression(feature, expressionBySite, activeOutcomes, activeWarnings);
            features.Add(feature);
        }

        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            var state = StateForDiscovery(npc.discoveryId);
            string streetId = npc.usualStreetId;
            string venueId = npc.usualVenueId;
            string subtitle = npc.usualLocationText;
            if (GugolNpcMapKnowledgeLedger.TryGet(npc.npcId, out var knowledge))
            {
                streetId = knowledge.streetId;
                venueId = knowledge.venueId;
                subtitle = GugolNpcMapKnowledgeLedger.FormatRecency(knowledge, dayKey);
                if (state != GugolMapKnowledgeState.Hidden) state = GugolMapKnowledgeState.LastKnown;
            }
            var feature = new GugolMapFeatureRecord
            {
                featureId = npc.npcId,
                kind = GugolMapFeatureKind.Npc,
                knowledgeState = state,
                displayName = npc.displayName,
                subtitle = subtitle,
                searchText = npc.displayName + " " + subtitle,
                streetId = streetId,
                venueId = venueId,
                npc = npc,
            };
            features.Add(feature);
        }

        var weather = FlorenceWeather.Instance;
        HubNode currentNode = hub != null ? hub.GetNode(DistrictTracker.CurrentNodeId) : null;
        string condition = weather != null ? weather.ConditionForNode(currentNode) : "clear";
        return new GugolMapKnowledgeSnapshot(features, condition, FlorenceWeather.FloodRiskToday, dayKey);
    }

    static Dictionary<string, List<GugolMapWorldExpressionDefinition>> BuildExpressionsBySite(
        GugolMapWorldExpressionDefinition[] definitions)
    {
        var result = new Dictionary<string, List<GugolMapWorldExpressionDefinition>>(StringComparer.Ordinal);
        foreach (var definition in definitions ?? Array.Empty<GugolMapWorldExpressionDefinition>())
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.siteId)) continue;
            if (!result.TryGetValue(definition.siteId, out var list))
                result.Add(definition.siteId, list = new List<GugolMapWorldExpressionDefinition>());
            list.Add(definition);
        }
        return result;
    }

    static void ApplyWorldExpression(
        GugolMapFeatureRecord feature,
        Dictionary<string, List<GugolMapWorldExpressionDefinition>> expressionBySite,
        SiteOutcomeRecord[] outcomes,
        CircleWarningRecord[] warnings)
    {
        if (feature == null || string.IsNullOrWhiteSpace(feature.siteId) ||
            !expressionBySite.TryGetValue(feature.siteId, out var expressions)) return;

        foreach (var expression in expressions)
        {
            if (expression == null) continue;
            if (expression.source == GugolMapExpressionSource.SiteOutcome)
            {
                foreach (var outcome in outcomes ?? Array.Empty<SiteOutcomeRecord>())
                {
                    if (outcome == null || !string.Equals(outcome.siteId, feature.siteId, StringComparison.Ordinal) ||
                        !string.Equals(outcome.outcomeId, expression.sourceId, StringComparison.Ordinal)) continue;
                    feature.expressionId = expression.expressionId;
                    feature.labelTreatment = expression.labelTreatment;
                    feature.knowledgeState = outcome.remembered
                        ? GugolMapKnowledgeState.RememberedLoss
                        : expression.labelTreatment == GugolMapLabelTreatment.Suppressed
                            ? GugolMapKnowledgeState.Forgotten
                            : GugolMapKnowledgeState.Lost;
                    if (outcome.remembered && !string.IsNullOrWhiteSpace(expression.rememberedText))
                        feature.subtitle = expression.rememberedText;
                }
            }
            else if (expression.source == GugolMapExpressionSource.Warning)
            {
                foreach (var warning in warnings ?? Array.Empty<CircleWarningRecord>())
                {
                    if (warning == null || warning.resolved || warning.expired ||
                        !string.Equals(warning.definitionId, expression.sourceId, StringComparison.Ordinal)) continue;
                    feature.expressionId = expression.expressionId;
                    feature.labelTreatment = expression.labelTreatment;
                }
            }
        }
    }

    static GugolMapKnowledgeState StateForDiscovery(string discoveryId)
    {
        if (string.IsNullOrWhiteSpace(discoveryId)) return GugolMapKnowledgeState.Known;
        return ExplorationDiscoveryLedger.GetStage(discoveryId) switch
        {
            DiscoveryStage.Hidden => GugolMapKnowledgeState.Hidden,
            DiscoveryStage.Rumored => GugolMapKnowledgeState.Rumored,
            _ => GugolMapKnowledgeState.Known,
        };
    }

    static string BuildVenueSubtitle(GugolVenueDefinition venue)
    {
        string services = string.Join(" · ", venue.services ?? Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(venue.openingHoursText)) return services;
        return string.IsNullOrWhiteSpace(services)
            ? venue.openingHoursText
            : venue.openingHoursText + " · " + services;
    }

    static string CurrentDayKey()
    {
        var calendar = GameCalendar.Instance;
        return calendar != null ? $"{calendar.Year}:{calendar.DayOfYear}" : "undated";
    }
}
