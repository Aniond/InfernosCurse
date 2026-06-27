# Inferno's Curse — Game Bible

## The Vision

A Lovecraftian JRPG set in 1200–1300 Italy, blending Dante Alighieri's *The Divine Comedy* with cosmic horror. The nine circles of Hell have begun to corrupt the towns and regions of medieval Italy, each circle manifesting its thematic evil in a different location. The player must travel between corrupted hubs, uncover the source of the spreading darkness, and descend deeper into the corruption.

---

## Setting

**Era:** 1200–1300 AD, medieval Italy  
**Aesthetic:** 2.5D HD-2D (Octopath Traveler style) — pixel art sprites in a 3D environment with URP lighting, depth of field, and bloom  
**Tone:** Dark, melancholic, oppressive beauty. The world is gorgeous but wrong. Corruption is subtle before it is violent.

---

## Source Material

- **The Divine Comedy** by Dante Alighieri — primary structural framework
- **Lovecraftian Mythos** — cosmic horror layer beneath the Dantean corruption
- **Historical Florence / Medieval Italy** — architecture, politics, culture, factions

---

## World Structure (Persona-Style Map System)

The world is not an open world. It uses a layered hub system:

```
Town Overview Map (stylized, clickable)
    └── Hub Areas (fully dressed 3D rooms — e.g. Market Square)
            └── Sub-Areas (story moments, encounters, NPCs)
```

This keeps scope manageable while giving the illusion of a living city. You never build all of Florence — you build the parts of Florence that matter to the story.

---

## Starting Location — Florence (Limbo)

### Why Florence
Dante himself wrote that Florence had lost its way and its soul. In *The Divine Comedy* he places countless Florentines in Hell — not out of hatred, but out of grief for what the city had become. The city that exiled him. The city he loved and condemned in the same breath.

Florence is the perfect starting zone because it carries Dante's original meaning: a city that is beautiful, historically significant, and spiritually hollow.

### The Corruption — Limbo (First Circle)
Limbo is the mildest circle — not violent punishment, but absence. The souls here are not evil, they are simply without. Without purpose. Without salvation. Without direction.

**How Limbo manifests in Florence:**
- Citizens going through the motions, having forgotten why they do what they do
- Merchants who've forgotten why they trade
- Priests who've forgotten why they pray
- A city drifting through its days, hollow without knowing it
- Nothing is overtly wrong — it just feels *off*
- The corruption is more unsettling than outright demonic evil because it's quiet

**The horror of Limbo is that the city doesn't know it's lost.**

### Florence Hub Areas (Planned)
- **Market Square** *(current build)* — the heart of the city, centered on a fountain
- **Church District** — the spiritual center, now going through empty rituals
- **Merchant Quarter** — wealth without purpose
- **The River Arno** — boundary between districts, liminal space
- **Noble Palazzo** — power without direction

---

## The Nine Circles — World Progression

Each circle corrupts a different town or region of medieval Italy. As the player descends, the corruption becomes more overt and violent.

| Circle | Sin | Location (TBD) | Manifestation |
|--------|-----|-----------------|---------------|
| 1 — Limbo | Absence of purpose | **Florence** | Hollow citizens, quiet wrongness |
| 2 — Lust | Uncontrolled desire | TBD | |
| 3 — Gluttony | Excess and waste | TBD | |
| 4 — Greed | Hoarding and spending | TBD | |
| 5 — Wrath | Anger and sullenness | TBD | |
| 6 — Heresy | False belief | TBD | |
| 7 — Violence | Against others, self, God | TBD | |
| 8 — Fraud | Deception and corruption | TBD | |
| 9 — Treachery | Betrayal of trust | TBD | |

---

## The Protagonist — Benidito

- Playable character, pixel art sprite
- Navigates the corrupting world like Dante navigating Hell itself
- A figure caught between the mortal world and the creeping darkness
- Background and full arc TBD

---

## Tone & Atmosphere

- **Limbo feels like a beautiful painting left out in the rain** — still lovely, increasingly wrong
- Deeper circles should feel progressively more alien and horrifying
- The Lovecraftian layer means the corruption isn't just theological — there is something *ancient* beneath Dante's cosmology. The circles of Hell may be a human attempt to understand something that predates human understanding.
- Music, lighting, and color grading shift per circle

---

## Technical Foundation

- **Engine:** Unity 6, URP 17.4.0
- **Camera:** Fixed-angle 2.5D, Cinemachine follow
- **Characters:** Pixel art sprites (SpriteRenderer in 3D), BottomCenter pivot, PPU=64
- **Environment:** GLB 3D assets (gltfast), URP Lit materials
- **Movement:** New Input System, 4-direction cardinal snap, Rigidbody 3D
- **Pipeline proven:** Sprite depth sorting, URP post-processing, GLB import, particle systems all working

---

## Reference Assets

- Lovecraft Mythos compendiums (Petersen's Guide, Malleus Monstrorum Vol I & II)
- Grand Grimoire of Cthulhu Mythos Magic
- Sun Tzu and Infernal Curse Game AI design notes
- Market Square map reference
- 3D asset library: Tuscan Townhouses, Florentine Buildings, Romanesque Church, Fountain, Cypress Trees, Barrels, Market Stalls, Sotoportico Archway
