// One-shot Mercato Vecchio commerce concepts for approval before Prism spend.
// Generates isolated source images only; it does not submit 3D jobs.
import { generateImage } from './gemini.mjs'
import { readFile, writeFile, mkdir } from 'node:fs/promises'
import path from 'node:path'

// Load local ignored credentials without printing them.
try {
  const env = await readFile(new URL('./.env', import.meta.url), 'utf8')
  for (const line of env.split(/\r?\n/)) {
    const match = line.match(/^\s*([^#=\s][^=]*)\s*=\s*(.*)\s*$/)
    if (match && process.env[match[1]] === undefined) {
      process.env[match[1]] = match[2].replace(/^["']|["']$/g, '')
    }
  }
} catch {
  // generateImage reports a clear missing-key error.
}

const STYLE = [
  'Production concept for a static 3D game environment prop in Inferno\'s Curse.',
  'Late medieval Florence around 1299, historically grounded timber construction, painterly stylized realism, hand-authored RPG look.',
  'Warm weathered wood, iron fasteners, woven natural fiber, faded natural-dye cloth, restrained saturation, PBR-readable materials.',
  'Single complete isolated object, centered, three-quarter front view, full object visible from feet to awning, plain white background.',
  'Soft even studio lighting, no cast shadow on background, no environment, no street, no floor slab, no people, no animals, no text, no signs, no watermark.',
  'Clean connected silhouette suitable for image-to-3D generation, practical game-asset proportions, approximately four metres wide.'
].join(' ')

const CONCEPTS = [
  {
    id: 'mercato-stall-bakery',
    description: 'A Florentine bakery market stall: sturdy open-front timber booth, shallow pitched faded ochre-and-cream canvas awning, integrated stepped wooden bread shelves and a broad serving counter, subtle carved joinery, small iron hooks, storage cubbies beneath. Shelves are mostly empty so separate bread baskets can be placed later.'
  },
  {
    id: 'mercato-stall-produce',
    description: 'A Florentine produce market stall: sturdy open-front timber booth, faded olive-green-and-cream canvas awning, integrated tiered display shelves sized for wicker baskets, broad front counter, angled lower produce racks, simple iron braces, storage cubbies beneath. Display areas are mostly empty so separate fruit and vegetable props can be placed later.'
  },
  {
    id: 'mercato-stall-cloth-dry-goods',
    description: 'A Florentine cloth and dry-goods market stall: elegant but practical timber booth, faded madder-red-and-cream canvas awning, integrated side racks and horizontal poles for folded cloth and hanging wares, broad counter, drawers and storage chest beneath, restrained carved merchant details. Racks are mostly empty so separate cloth, bags, baskets, and lantern props can be placed later.'
  },
  {
    id: 'mercato-handcart',
    description: 'A single medieval Florentine two-wheeled merchant handcart: weathered oak bed with raised side rails, two large iron-rimmed wooden wheels, long paired pulling handles, simple iron brackets, small fold-down rear support leg, empty cargo bed, compact and sturdy, no horse harness.'
  }
]

const output = path.resolve('output', 'concepts', 'mercato-commerce')
await mkdir(output, { recursive: true })

for (const concept of CONCEPTS) {
  const image = await generateImage(`${concept.description} ${STYLE}`)
  const destination = path.join(output, `${concept.id}.png`)
  await writeFile(destination, image)
  console.log(`OK ${concept.id} (${image.length} bytes)`)
}
