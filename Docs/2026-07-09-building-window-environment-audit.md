# Building Window Environment Audit

Date: 2026-07-09

## Shared State

- `GameClock` remains the only time source.
- `FlorenceWeather.CurrentProfileName` remains the weather source.
- COZY remains responsible for sun, sky, precipitation, and thunder spawning.
- `WorldWindowEnvironment` is a read-only presentation adapter.
- Lightning response samples the actual light spawned under COZY's thunder FX parent. No independent indoor or building lightning timer exists.

## Implemented Coverage

| Scene or building set | Integration | Result |
|---|---|---|
| FlorentineInnFloor1 | Ten framed interior-looking-out windows, five time-driven lamps, ten exterior rain emitters, and ten fog overlays | Clear, cloudy, rain/storm, fog, dawn/day/dusk/night, and authoritative lightning response |
| PonteVecchio | Two window-gap groups derived from the actual pier spacing in the north windowed walls | Occupied-window time/weather/lightning response without covering the masonry piers |
| MercatoVecchio | Eleven monolithic building façades | Forty optimized single-renderer four-pane windows generated at runtime |
| ViaCalimala | Fourteen reused market-building façades | Shared façade behavior; future street-template rebuilds reapply the installer |
| Fiesole | Forty modular front-wall façades | Front walls only; roof, ceiling, foundation, and higher LOD renderers are excluded |
| PiazzaDellaSignoria | Nine monolithic building façades | Shared façade behavior through scene-local components |
| GiardinoDelleRose | Gardener's cottage | Shared façade behavior |
| SaloneDelleArti | Existing clerestory `LightShaft` system | Shaft brightness now also follows the persistent global weather profile |

## Documented Skips

- `Duomo`: the current scene exposes no independently usable window renderer and no `LightShaft` component. Existing lights and global COZY environment continue to reflect world time and weather; no arbitrary window cards were added.
- Buildings without a renderer large enough to represent a usable façade are skipped.
- Higher LOD, roof, ceiling, foundation, particle, trail, and line renderers are skipped.

## Builder Persistence

The installer is invoked by the Fiesole, Giardino delle Rose, Giardino walled garden, Piazza della Signoria, Salone delle Arti, and street-template builders. Rebuilding these scenes therefore restores the shared façade configuration. Ponte Vecchio and the existing non-builder scenes are covered by the explicit installer menu pass.

## Runtime Evidence

- Inn clear-night test: five lamps at configured night intensity, no exterior weather overlays active.
- Inn wet/storm test: ten exterior rain systems active and lamps reduced by daytime/weather response.
- Inn fog test: ten fog overlays active and all rain systems inactive.
- Mercato Vecchio night capture: forty windows across eleven façade components, rendered as restrained amber four-pane windows.
- Mercato Vecchio emission sample: clear night `RGBA(0.450, 0.288, 0.135, 0.450)`; daytime wet/storm `RGBA(0.006, 0.004, 0.002, 0.006)`.
- Unity script validation reported no diagnostics for the façade generator or installer. Unity compilation reported zero errors.

## Asset Inventory Note

Asset Inventory is connected, but only one of 193 packages is currently indexed. Broad prop searches therefore returned no candidates. Live-project search confirmed reusable barrels and fountain assets; the prop selection pass should continue after indexing more owned packages or by searching the installed project directly.
