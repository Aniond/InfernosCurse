// Approved 2026-07-12 Mercato commerce batch.
// Submits four concept images to 3D AI Studio Prism 3.1, journals task IDs,
// polls to completion, and downloads recoverable GLBs.
import { readFile, writeFile, mkdir, access } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { imageToModel } from './tripo.mjs'

try {
  const env = await readFile(new URL('./.env', import.meta.url), 'utf8')
  for (const line of env.split(/\r?\n/)) {
    const match = line.match(/^\s*([^#=\s][^=]*)\s*=\s*(.*)\s*$/)
    if (match && process.env[match[1]] === undefined) {
      process.env[match[1]] = match[2].replace(/^["']|["']$/g, '')
    }
  }
} catch {
  // tripo.mjs reports the missing key without exposing credentials.
}

const root = path.dirname(fileURLToPath(import.meta.url))
const manifestPath = path.join(root, 'mercato-commerce.json')
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
const outputDir = path.join(root, 'output', 'models', 'mercato-commerce')
const statePath = path.join(outputDir, 'batch-state.json')
await mkdir(outputDir, { recursive: true })

let state = { startedAt: new Date().toISOString(), assets: {} }
try { state = JSON.parse(await readFile(statePath, 'utf8')) } catch { /* fresh batch */ }

async function persist() {
  state.updatedAt = new Date().toISOString()
  await writeFile(statePath, JSON.stringify(state, null, 2))
}

async function exists(file) {
  try { await access(file); return true } catch { return false }
}

await Promise.all(manifest.map(async (asset) => {
  const destination = path.join(outputDir, `${asset.id}.glb`)
  if (await exists(destination)) {
    console.log(`[skip] ${asset.id}: GLB already exists`)
    return
  }

  state.assets[asset.id] = {
    ...(state.assets[asset.id] || {}),
    name: asset.name,
    role: asset.role,
    concept: asset.concept,
    prompt: asset.prompt,
    status: 'submitting'
  }
  await persist()

  try {
    const concept = await readFile(path.join(root, asset.concept))
    let lastStatus = ''
    const model = await imageToModel(concept, {
      intervalMs: 5000,
      timeoutMs: 12 * 60 * 1000,
      onProgress: (progress) => {
        if (progress.taskId) state.assets[asset.id].taskId = progress.taskId
        if (progress.status) state.assets[asset.id].status = progress.status.toLowerCase()
        const status = `${progress.status || ''}:${progress.progress ?? ''}`
        if (status !== lastStatus) {
          lastStatus = status
          console.log(`[${asset.id}] ${status}`)
        }
        void persist()
      }
    })
    await writeFile(destination, model)
    state.assets[asset.id].status = 'done'
    state.assets[asset.id].bytes = model.length
    state.assets[asset.id].completedAt = new Date().toISOString()
    console.log(`[done] ${asset.id}: ${model.length} bytes`)
  } catch (error) {
    state.assets[asset.id].status = 'failed'
    state.assets[asset.id].error = String(error?.message || error)
    console.error(`[failed] ${asset.id}: ${state.assets[asset.id].error}`)
  }
  await persist()
}))

const failed = Object.values(state.assets).filter((asset) => asset.status === 'failed')
console.log(`Mercato commerce batch complete: ${manifest.length - failed.length}/${manifest.length} successful`)
if (failed.length) process.exitCode = 1
