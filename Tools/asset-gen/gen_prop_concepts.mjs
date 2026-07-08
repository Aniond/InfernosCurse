// One-shot: battle-map prop concepts (rocks + Tuscan trees) for David's
// approval before any 3D spend. Writes full PNGs + ~600px JPEG chat copies.
import { generateImage } from './gemini.mjs'
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'


const STYLE = 'Painterly stylized-realism game prop concept, hand-painted look matching a bright green Tuscan meadow battle map (Final Fantasy Tactics remaster style). Single isolated prop, centered, soft neutral light-grey background, no scene, no dramatic lighting, full object visible.'

const PROPS = [
  ['rock-boulder-cluster', 'Cluster of three weathered white-grey limestone boulders, cracked facets, moss at the base, small grass tufts. 1299 Tuscany.'],
  ['rock-outcrop-tall', 'Tall angular white limestone outcrop slab, chest-height standing stone, lichen patches, mossy foot. 1299 Tuscany.'],
  ['tree-cypress-battle', 'Slender Italian cypress tree, dark green flame silhouette, slightly wind-leaned, small mossy root mound. 1299 Tuscany.'],
  ['tree-stone-pine-battle', 'Mediterranean stone pine, umbrella canopy, reddish trunk, gnarled roots on a grassy mound. 1299 Tuscany.'],
]

mkdirSync('output/concepts', { recursive: true })
for (const [id, desc] of PROPS) {
  const png = await generateImage(`${desc} ${STYLE}`)
  writeFileSync(`output/concepts/${id}.png`, png)
  console.log(`OK ${id} (${png.length} bytes)`)
}
