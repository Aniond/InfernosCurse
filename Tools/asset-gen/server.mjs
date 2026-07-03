// Asset-gen workbench for Inferno's Curse.
//
// Workflow: maintain a list of assets each with an image prompt -> generate a Gemini
// concept image per asset (regenerate with extra prompt text if needed) -> approve or
// deny -> one button submits every approved image to 3D AI Studio (Tripo P1), spaced
// to obey their 3-requests/minute cap -> preview each finished GLB in Three.js.
//
// Zero dependencies: Node 20 http + vanilla JS UI. Run:  node server.mjs
// Keys live in .env next to this file (gitignored):
//   GEMINI_API_KEY=...
//   3D_AI_STUDIO_API_KEY=...

import http from 'http'
import fs from 'fs/promises'
import path from 'path'
import { fileURLToPath } from 'url'
import { generateImage, hasGeminiKey } from './gemini.mjs'
import { imageToModel, hasTripoKey } from './tripo.mjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const PORT = Number(process.env.ASSET_GEN_PORT) || 3311
const DATA_FILE = path.join(__dirname, 'assets.json')
const IMG_DIR = path.join(__dirname, 'output', 'images')
const MODEL_DIR = path.join(__dirname, 'output', 'models')

// ── .env loader (no dotenv dep) ─────────────────────────────────────────────
try {
  const env = await fs.readFile(path.join(__dirname, '.env'), 'utf8')
  for (const line of env.split(/\r?\n/)) {
    const m = line.match(/^\s*([^#=\s][^=]*)\s*=\s*(.*)\s*$/)
    if (m && process.env[m[1]] === undefined) process.env[m[1]] = m[2].replace(/^["']|["']$/g, '')
  }
} catch { /* no .env yet — key warnings surface in the UI */ }

// ── state ────────────────────────────────────────────────────────────────────
// asset: { id, name, prompt, extraPrompt, status: 'new'|'generated'|'approved'|'denied',
//          hasImage, model: null|'queued'|'submitting'|'generating'|'done'|'failed',
//          taskId, error }
let assets = []
try { assets = JSON.parse(await fs.readFile(DATA_FILE, 'utf8')) } catch { assets = [] }
async function persist() {
  await fs.writeFile(DATA_FILE, JSON.stringify(assets, null, 2))
}
const byId = (id) => assets.find((a) => a.id === id)
const slug = (s) => s.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'asset'

let batch = { running: false, total: 0, done: 0, failed: 0 }

// ── helpers ──────────────────────────────────────────────────────────────────
async function readBody(req) {
  const chunks = []
  for await (const c of req) chunks.push(c)
  const raw = Buffer.concat(chunks).toString('utf8')
  return raw ? JSON.parse(raw) : {}
}
function json(res, code, obj) {
  res.writeHead(code, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify(obj))
}
async function sendFile(res, file, type) {
  try {
    const data = await fs.readFile(file)
    res.writeHead(200, { 'Content-Type': type, 'Cache-Control': 'no-store' })
    res.end(data)
  } catch {
    res.writeHead(404); res.end('not found')
  }
}

// ── actions ──────────────────────────────────────────────────────────────────
async function doGenerateImage(asset) {
  const fullPrompt = asset.extraPrompt?.trim()
    ? `${asset.prompt.trim()}\n\nAdditional direction: ${asset.extraPrompt.trim()}`
    : asset.prompt.trim()
  const png = await generateImage(fullPrompt)
  await fs.mkdir(IMG_DIR, { recursive: true })
  await fs.writeFile(path.join(IMG_DIR, `${asset.id}.png`), png)
  asset.hasImage = true
  asset.status = 'generated'
  asset.error = null
  await persist()
}

async function runBatch() {
  const targets = assets.filter((a) => a.status === 'approved' && a.hasImage && a.model !== 'done')
  batch = { running: true, total: targets.length, done: 0, failed: 0 }
  for (const a of targets) { a.model = 'queued'; a.error = null }
  await persist()
  await fs.mkdir(MODEL_DIR, { recursive: true })

  // Fire all in parallel: the tripo module's internal gate spaces SUBMISSIONS >=21s
  // apart (3/min), while polling of already-submitted tasks runs concurrently.
  await Promise.allSettled(targets.map(async (a) => {
    try {
      a.model = 'submitting'; await persist()
      const png = await fs.readFile(path.join(IMG_DIR, `${a.id}.png`))
      const glb = await imageToModel(png, {
        onProgress: (p) => {
          if (p.taskId) a.taskId = p.taskId
          if (p.status && p.status !== 'SUBMITTED') a.model = 'generating'
        }
      })
      await fs.writeFile(path.join(MODEL_DIR, `${a.id}.glb`), glb)
      a.model = 'done'
      batch.done++
    } catch (e) {
      a.model = 'failed'
      a.error = String(e.message || e)
      batch.failed++
      console.error(`[batch] ${a.name}: ${a.error}`)
    }
    await persist()
  }))
  batch.running = false
}

// ── routes ───────────────────────────────────────────────────────────────────
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`)
  const p = url.pathname
  try {
    if (p === '/' && req.method === 'GET')
      return sendFile(res, path.join(__dirname, 'index.html'), 'text/html')

    if (p === '/api/state' && req.method === 'GET')
      return json(res, 200, { assets, batch, keys: { gemini: hasGeminiKey(), tripo: hasTripoKey() } })

    if (p === '/api/assets' && req.method === 'POST') {
      const b = await readBody(req)
      if (!b.name?.trim() || !b.prompt?.trim()) return json(res, 400, { error: 'name and prompt required' })
      let id = slug(b.name), n = 2
      while (byId(id)) id = `${slug(b.name)}-${n++}`
      assets.push({ id, name: b.name.trim(), prompt: b.prompt.trim(), extraPrompt: '', status: 'new', hasImage: false, model: null, taskId: null, error: null })
      await persist()
      return json(res, 200, { ok: true, id })
    }

    const m = p.match(/^\/api\/assets\/([\w-]+)\/(update|generate|approve|deny|delete)$/)
    if (m && req.method === 'POST') {
      const a = byId(m[1])
      if (!a) return json(res, 404, { error: 'no such asset' })
      const action = m[2]
      if (action === 'update') {
        const b = await readBody(req)
        if (typeof b.prompt === 'string') a.prompt = b.prompt
        if (typeof b.extraPrompt === 'string') a.extraPrompt = b.extraPrompt
        if (typeof b.name === 'string' && b.name.trim()) a.name = b.name.trim()
        await persist()
        return json(res, 200, { ok: true })
      }
      if (action === 'generate') {
        const b = await readBody(req)
        if (typeof b.extraPrompt === 'string') a.extraPrompt = b.extraPrompt
        await doGenerateImage(a)   // sync: caller sees the finished image on reload
        return json(res, 200, { ok: true })
      }
      if (action === 'approve') { a.status = 'approved'; await persist(); return json(res, 200, { ok: true }) }
      if (action === 'deny') { a.status = 'denied'; await persist(); return json(res, 200, { ok: true }) }
      if (action === 'delete') {
        assets = assets.filter((x) => x.id !== a.id)
        await persist()
        return json(res, 200, { ok: true })
      }
    }

    if (p === '/api/generate-models' && req.method === 'POST') {
      if (batch.running) return json(res, 409, { error: 'batch already running' })
      runBatch() // fire and forget; UI polls /api/state
      return json(res, 200, { ok: true })
    }

    if (p.startsWith('/images/'))
      return sendFile(res, path.join(IMG_DIR, path.basename(p)), 'image/png')
    if (p.startsWith('/models/'))
      return sendFile(res, path.join(MODEL_DIR, path.basename(p)), 'model/gltf-binary')

    const pv = p.match(/^\/preview\/([\w-]+)$/)
    if (pv) {
      const a = byId(pv[1])
      if (!a || a.model !== 'done') { res.writeHead(404); return res.end('model not ready') }
      const html = (await fs.readFile(path.join(__dirname, 'preview.html'), 'utf8'))
        .replaceAll('__MODEL_URL__', `/models/${a.id}.glb`)
        .replaceAll('__NAME__', a.name)
      res.writeHead(200, { 'Content-Type': 'text/html' })
      return res.end(html)
    }

    res.writeHead(404); res.end('not found')
  } catch (e) {
    console.error(e)
    json(res, 500, { error: String(e.message || e) })
  }
})

server.listen(PORT, () => {
  console.log(`asset-gen workbench: http://localhost:${PORT}`)
  console.log(`keys: gemini=${hasGeminiKey()} tripo=${hasTripoKey()}`)
})
