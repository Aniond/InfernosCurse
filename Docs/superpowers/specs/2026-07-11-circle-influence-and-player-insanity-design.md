# Circle Influence and Player Insanity Separation Design

Date: 2026-07-11
Status: Approved design

## Summary

Inferno's Curse separates supernatural world pressure from Benidito's personal use of corrupted power.

- **Circle Influence** is hidden world state. It belongs to cities, towns, villages, estates, wineries, monasteries, and other named territories on the Gugol map. Player choices, NPC actions, cultists, rituals, institutional failures, resistance, and propagation between territories change it. It drives story events, NPC behavior, environmental change, encounters, and permanent consequences.
- **Insanity** is hidden player-only state. It is calculated exclusively from Benidito's currently equipped, unrefined Corrupted Skills. It drives personal audiovisual distortion and deterministic combat penalties.

The two domains are completely isolated. They do not read, scale, modify, or write one another. Equipping, using, refining, or unequipping a Corrupted Skill can never alter Circle Influence. World conditions can never alter Insanity.

## Design Principles

1. **World state is not player state.** A city can be collapsing while Benidito is clear-minded, and Benidito can be deeply unstable while standing in a healthy territory.
2. **Hidden does not mean arbitrary.** Neither system exposes a number, but every meaningful transition has observable consequences.
3. **Exploration is free; neglect is a choice.** Ordinary movement and time passage do not create Circle Influence. Unresolved sources continue acting while time passes, and warned crises can expire.
4. **Pressure is recoverable; history is not.** Circle Influence can be reduced through meaningful action. Deaths, destroyed places, failed opportunities, and other Chronicle facts remain permanent.
5. **The game owns facts.** Gemini may phrase reactions, but authored definitions, the World Event Ledger, and the Campaign Chronicle own state changes.

## State Architecture

### Circle World State

The Circle domain owns:

- Per-territory, per-Circle hidden influence values.
- Territory identity, parent region, native Circle, importance weight, resistance, and route connections.
- Active world sources and their processed-day keys.
- Derived regional pressure.
- Threshold eligibility, warnings, crisis stages, and deadlines.
- NPC Circle overlays and permanent site outcomes.
- Circle-specific battle and environmental presentation eligibility.

Its accepted inputs are registered world consequences from authored choices, persistent world agents, NPC action, rituals, institutions, resistance, and daily propagation.

### Player Insanity State

The Insanity domain owns no independent persistent meter. It calculates the current value from Benidito's saved skill loadout and exposes the resulting personal presentation and combat modifiers.

Its only accepted inputs are equipped `AbsorbedSkillInstance` records whose original skill is corrupted and not refined.

### Hard Boundary

There is no shared total and no conversion rule. The production architecture must enforce all of the following:

- Circle services cannot query Insanity, the equipped orb loadout, or Insanity tiers.
- Insanity services cannot query territories, regions, Circles, NPC state, or Chronicle events.
- Encounter generation may read Circle World State, but it cannot query or scale from Benidito's Insanity.
- Insanity cannot change because of a territory's Circle value or dominant Circle.
- No authored event may bridge the two systems, even deliberately.

Editor-only diagnostics may inspect both domains in one report, but runtime gameplay logic may not combine them.

## Territory and Regional Model

### Gugol as the Authoritative Graph

The Gugol map defines every state-bearing territory. A territory record contains:

- Stable territory ID.
- Parent-region ID.
- Territory type, such as city, town, village, estate, winery, monastery, or landmark.
- Native Circle.
- Regional importance weight greater than zero.
- Route connections with propagation strengths.
- Authored sanctity or Circle resistance.
- Zero or more independent Circle values.

Florence is one citywide territory inside Tuscany. Florence does not maintain separate numeric values for Mercato, Giardino, Signoria, or other sites within the city. Those places may have permanent authored outcome states, but they read Florence's citywide Circle values.

A separately mapped village, monastery, or winery outside Florence may own its own Circle values and contribute independently to Tuscany.

### Independent Circles

Every influence value is internally clamped from 0 to 1. Values do not have to sum to 1. A territory can carry its native Circle and one or more invading Circles simultaneously.

The native Circle controls the territory's intended primary arc. Other Circles can arrive only through an authored source or propagation from a connected territory. An invading Circle retains its own identity, symptoms, event eligibility, and recovery path.

### Derived Regional Pressure

Regions do not store mutable Circle values. Their pressure is recalculated from member territories:

```text
regionalPressure(circle) =
    sum(territoryInfluence(circle) x territoryRegionalWeight)
    / sum(territoryRegionalWeight)
```

Major cities therefore matter more than isolated estates, while several failing towns or villages can collectively endanger a region. Regional events read the derived result. No event writes directly to a region.

## Circle Sources and Recovery

### Valid Sources

Circle Influence may increase through:

- Authored player choices, refusals, or failed opportunities.
- Persistent cultists and other world agents.
- Rituals, relics, supernatural sites, and active Circle phenomena.
- NPC collaboration with a Circle or collapse of local resistance.
- Failed institutions and permanent site outcomes.
- Propagation from connected territories.

Every change carries a stable event or source ID and an exact authored value. Daily sources apply once per source and game-date key. Retrying, loading, revisiting a scene, or receiving duplicate Gemini output cannot apply the same change twice.

### Valid Reductions

Circle Influence may decrease through:

- Defeating or permanently disabling a source.
- Disrupting a source for an authored duration.
- Saving exposed NPCs.
- Restoring institutions or community resistance.
- Completing remembrance, reconciliation, sanctuary, or reclamation events.
- Other explicit registered world actions.

Circle Influence never decays merely because time passed or because active sources disappeared. Rest and travel do not directly increase or decrease it. They advance the day, allowing unresolved sources, crisis deadlines, and propagation to process.

## Cross-Territory Propagation

Propagation uses the approved high-pressure model. It begins only when a source exceeds 70% in a Circle.

```text
sourcePressure = clamp01((sourceInfluence - 0.70) / 0.30)
gradient = clamp01((sourceInfluence - targetInfluence) / 0.25)
resistanceBlock = clamp01(1 - targetResistance x circleResistanceCoefficient)

dailyBleed = 0.012
    x sourcePressure
    x gradient
    x routeStrength
    x resistanceBlock
```

Default route strengths are:

- Strong direct road or neighboring territory: 1.00.
- Longer regional connection: 0.75.
- Weak or indirect connection: 0.50.

Incoming propagation of one Circle is capped at 0.015 per target territory per day. Export does not reduce the source. A beginning-of-day snapshot supplies every calculation, and all results apply as one batch, preventing a Circle from cascading through multiple territories during one day.

These formulas and values are never shown to the player.

## Hidden Thresholds and Fair Warnings

Circle profiles and authored events define internal thresholds. Crossing a threshold never directly executes an irreversible loss.

Instead, the threshold makes a warning event eligible. A warning event contains:

- Stable event ID, territory ID, Circle ID, and severity.
- Observable opening symptom.
- Escalating NPC, rumor, environmental, and site-presentation stages.
- Authored response paths.
- First-seen day and internal deadline.
- Success, refusal, and expiration outcomes.
- Permanent and recoverable consequences.

The player never sees a countdown. Dialogue, behavior, rumor, and visible deterioration must communicate urgency clearly. Major irreversible losses receive several escalating signs over multiple in-game days or rests. Continuing to sleep, travel, or pursue other goals after those warnings is a meaningful decision.

At most one new major warning may open in a territory on one game day. This prevents threshold spam and gives each crisis room to be understood.

## Recoverable Territories and Permanent Scars

A territory at maximum Circle Influence remains explorable and can be recovered. Maximum pressure does not permanently delete a city or remove it from the map.

Recovery does not reverse Chronicle facts. The following may remain permanent:

- NPC deaths.
- Destroyed or transformed sites.
- Lost recruitment opportunities.
- Political and faction changes.
- Broken relationships.
- Failed historical opportunities.

Lowering Circle Influence prevents additional deterioration and removes recoverable Circle overlays. It does not resurrect people or restore destroyed places.

### Giardino delle Rose Example

The Florist crisis is the reference example for a site inside a citywide territory:

1. Florence's hidden Circle state and authored prerequisites make the crisis eligible.
2. The garden visibly declines and the Florist asks Benidito for help.
3. Continued delay produces worsening plant damage and increasingly explicit NPC concern.
4. Expiration permanently kills the recruitable Florist-associated NPC, records the garden as lost, changes Giardino's presentation, and increases Florence's applicable Circle Influence.
5. Connected NPCs grieve, blame, change routines, and remember the loss.
6. If Limbo later worsens, memories fragment until citizens may deny the garden ever existed.
7. Reducing Limbo stops further memory erosion.
8. An authored act of remembrance restores the truth and grief.
9. The recruit remains dead and the original garden never returns.

The garden has a permanent site-outcome state, not a separate numeric Circle meter.

## Circle Expression Profiles

Every Circle definition provides a reusable expression profile containing:

- Eligible event and rumor tags.
- NPC affliction stages and recovery rules.
- Environmental and site-presentation symptoms.
- Encounter and battle-presentation themes.
- Vocabulary for authored and Gemini-assisted narrative.
- Resistance and propagation tuning.

The shared system owns state, thresholds, persistence, and scheduling. The Circle profile owns how that pressure manifests.

### Limbo Expression

Limbo represents absence, disconnection, lost identity, and forgotten history.

- **Early:** hesitation, wrong names, repeated conversations, misplaced objects, and uncertain familiarity.
- **Growing:** missed routines, lost destinations, empty workplaces, incomplete records, and weakening relationships.
- **Severe:** people, traditions, and familiar places disappear from communal memory.
- **Recovery:** meaningful acts can restore facts and grief, but permanent deaths and destroyed places remain.

### NPC Relevance Layers

NPC response scales by relevance rather than giving every citizen identical bespoke state:

1. Directly connected NPCs receive persistent individual reactions, relationship changes, schedule changes, grief, blame, and later memory erosion.
2. Local witnesses receive shared rumors, ambient dialogue, mood, and behavior overlays.
3. The wider territory receives public symptoms such as empty services, altered records, changed celebrations, or damaged common spaces.
4. The region reacts only when weighted regional pressure makes regional events eligible.

NPC Circle effects are non-destructive overlays. Original identities, relationships, schedules, services, and authored dialogue remain preserved beneath them unless a Chronicle event permanently changes those facts.

## Player Insanity

### Calculation

Insanity is a hidden integer from 0 to 100:

```text
insanity = clamp(
    sum(equippedUnrefinedSkill.insanityCost x slottedOrbLevel),
    0,
    100)
```

Only Benidito's equipped absorbed-skill slots contribute. Banked or unequipped orbs contribute zero. A refined skill contributes zero permanently. Unequipping a skill removes its contribution immediately. Rest, travel, time, NPCs, story events, and Circle Influence do not affect the value.

### Tiers

| Hidden range | Personal presentation | Benidito-only mechanic |
|---|---|---|
| 0-14 | Normal perception | None |
| 15-34 | Subtle vignette and uneasy ambience | None; warning tier |
| 35-54 | Water and lighting distortion; uncertain distant sounds | Perception -2 |
| 55-74 | Whispers, visual echoes, and stronger distortion | Perception -3; Faith -2 |
| 75-100 | Severe cosmetic hallucination layers | Perception -4; Faith -4; CT gain -10% |

Penalties apply after equipment bonuses. Corrupted orbs already supply their approved skill power and stat benefits, so the tier penalties create the predictable cost of carrying too much power.

The production UI shows no Insanity number, bar, tier name, icon, or cost total. Editor diagnostics and automated tests may display the exact value.

### Player-Agency Boundaries

Insanity never:

- Alters territories, regions, Circles, NPCs, quests, rumors, or Chronicle events.
- Changes encounter chance, enemy population, or enemy awareness outside battle.
- Creates false choices, fake interactables, fake objectives, or inaccurate combat forecasts.
- Forces movement, actions, targeting, lost turns, or random skill failure.
- Produces an automatic game over at 100.

Hallucination presentation may use non-interactive silhouettes, audio, color, vignettes, and visual echoes, but it cannot misrepresent an actionable game fact.

## Feature Controls

The current all-purpose `GameFeatures.CorruptionEnabled` switch is replaced by independent controls:

- Circle simulation and story consequences.
- Player Insanity calculation.
- Insanity audiovisual presentation.
- Circle-specific battle presentation.

Disabling one control cannot disable or enable another domain implicitly. Production defaults enable the approved Circle simulation and Insanity calculation. Presentation controls remain independently tunable during visual review.

## Daily Processing Order

At each game-day transition, the world domain processes in this order:

1. Validate the date key and skip already processed work.
2. Apply unresolved local sources and NPC resistance once.
3. Snapshot all territory Circle values.
4. Calculate and batch-apply route propagation.
5. Recalculate regional pressure.
6. Advance active warning stages and commit expired outcomes.
7. Evaluate thresholds and enqueue eligible warnings.
8. Refresh NPC overlays, site outcomes, environmental presentation, encounter eligibility, and Gemini context.

Player choices can commit registered world consequences immediately. The daily pass remains deterministic and idempotent.

## Persistence and Campaign Permanence

Ordinary saves store:

- Territory, Circle, and value arrays.
- Processed source and propagation day keys.
- Persistent world-agent states.
- Active warning IDs, stages, opening dates, and deadlines.
- NPC Circle overlays and recovery state.
- Permanent site-outcome materializations.
- The existing World Event Ledger.

Regional pressure is recalculated and not saved. Insanity is recalculated from the saved loadout and not saved separately.

Campaign-permanent choices, deaths, destroyed sites, lost recruitment opportunities, and remembrance outcomes commit to the Campaign Chronicle before their aftermath appears. Loading an older slot reconciles those facts forward exactly once.

### Legacy Migration

Legacy Florence district corruption values collapse into one Florence Limbo value through a fixed, validated migration-weight table. The migration runs only when citywide Circle data is absent. Unknown legacy nodes follow the existing guard-and-skip policy, and malformed parallel arrays reject the import instead of silently inventing state.

Existing equipped orb data remains the sole migration source for Insanity. No old world-corruption value migrates into the player domain.

## Gemini Boundary

Gemini may:

- Phrase dialogue, grief, rumors, reactions, and memory fragmentation.
- Reference canonical prior choices and site outcomes.
- Select only from eligible authored follow-up IDs.

Gemini may not:

- Read or reveal hidden numeric Circle or Insanity values to the player.
- Invent territories, Circles, sources, events, choices, deadlines, deltas, deaths, or recoveries.
- Apply state changes.
- Contradict the World Event Ledger or Campaign Chronicle.

Timeout, malformed output, unknown IDs, quota failure, or unavailable networking uses deterministic authored fallback with identical state results.

## Failure Handling

- Unknown territory, region, Circle, source, event, or choice IDs are rejected before mutation.
- Non-finite values, invalid weights, malformed arrays, and out-of-range deltas fail validation.
- A missing warning definition cannot cause a silent permanent loss. The threshold remains eligible and reports an authoring error.
- A failed Chronicle write blocks the irreversible outcome and its aftermath presentation.
- Duplicate source, event, choice, propagation, or daily-processing keys become safe no-ops.
- A missing Insanity presentation asset suppresses only that cosmetic layer. It cannot change the calculated tier or battle modifiers.
- A missing Circle profile suppresses its presentation and events with a production validation failure; it does not substitute another Circle's behavior.

## Verification

### Isolation

- Changing any Circle value leaves Insanity, loadout, personal stats, and personal presentation unchanged.
- Equipping, leveling, refining, and unequipping every corrupted skill leaves territories, regions, NPC state, encounter eligibility, and event state unchanged.
- Static dependency checks reject runtime references from Circle services to Insanity services and from Insanity services to world-state services.

### World Simulation

- Territory weighting produces exact regional results.
- Independent Circles coexist without normalization or overwrite.
- Propagation remains zero at or below 70%, follows route and resistance tuning above it, respects gradient and caps, and cannot cascade in one day.
- Source-free exploration and rest produce no direct influence change.
- Meaningful reductions apply once and no passive decay occurs.
- Thirty-, sixty-, and one-hundred-twenty-day simulations create escalating regional danger without an unstoppable all-map collapse.

### Warnings and Permanence

- Hidden thresholds open warnings rather than committing losses.
- Warning escalation and expiration survive save/load and older-slot reconciliation.
- Giardino success, delay, loss, grief, erasure, reduced-Limbo stabilization, and remembrance preserve the approved facts.
- The lost recruit and original garden never return after failure.
- Chronicle failure cannot display a false aftermath.

### Insanity

- Every boundary value from 0 through 100 selects the correct tier.
- Loadout changes update Insanity and penalties immediately.
- Refinement permanently removes the skill's contribution.
- Rest, time, Circle state, and world events never change Insanity.
- No Insanity tier alters encounter rates, NPCs, story state, control, action availability, or factual UI.

### Runtime Proof

- Play-mode tests cover one clean Florence opening, one escalating Crier source, the Florist warning chain, a permanent Giardino loss, remembrance after Limbo reduction, and multiple invading Circles.
- Player-only tests cover audiovisual tier transitions and deterministic battle penalties.
- Final validation ends with zero new first-party errors or warnings attributable to this separation.

## Required Compatibility Changes

The implementation plan must account for these existing couplings:

- Remove `InsanityState.WorldCorruption()` from player-state calculation and delete the `Personal + WorldCorruption` total behavior.
- Make `InsanityPresenter` read personal loadout Insanity only.
- Remove Insanity-based road encounter and zone-detection modifiers.
- Stop rest from directly adding or cleansing Circle Influence; rest advances time, while authored sources and actions own changes.
- Replace the monolithic corruption feature switch with the independent controls defined above.
- Collapse Florence sub-city numeric state into its citywide territory while preserving permanent site outcomes.

## Out of Scope

- Designing the full quest content, rewards, dialogue, art, or exact Circle delta for the Florist chain.
- Implementing complete expression profiles for the other eight Circles.
- Exposing either hidden value through a meter, percentage, debug-like map overlay, or player-facing tier label.
- Random loss of player control or irreversible personal Insanity damage.
- Implementation work; this document defines the contract that the implementation plan must follow.
