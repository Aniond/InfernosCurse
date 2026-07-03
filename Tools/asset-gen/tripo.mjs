// 3D AI Studio image-to-3D client — PRISM 3.1 (the Studio UI's default model;
// the API exposes it as Tripo v3.1). Cheapest TEXTURED tier: 40 base + 20 for
// standard texture = 60 credits/model (PBR included; "detailed" texture = +40).
// Submit an approved concept image, poll to completion, download the resulting GLB.
//
// Key: process.env['3D_AI_STUDIO_API_KEY'] (env name starts with a digit, so it can
// only be read via bracket access).

import fs from 'fs/promises'
import path from 'path'
import { fileURLToPath } from 'url'

const BASE = 'https://api.3daistudio.com/v1'
const GENERATE_URL = `${BASE}/3d-models/tripo/image-to-3d/3.1/` // Prism 3.1
const statusUrl = (taskId) => `${BASE}/generation-request/${encodeURIComponent(taskId)}/status/`

// Persist every submit + final status payload so a response is recoverable even on a
// crash/disconnect (credits are spent at submit time). JSONL + per-task JSON files.
const __dirname = path.dirname(fileURLToPath(import.meta.url))
const LOG_DIR = path.resolve(__dirname, 'output', '_tripo')
async function logResponse(kind, taskId, response) {
  try {
    await fs.mkdir(LOG_DIR, { recursive: true })
    const stamp = new Date().toISOString()
    await fs.appendFile(
      path.join(LOG_DIR, 'responses.jsonl'),
      JSON.stringify({ ts: stamp, kind, taskId, response }) + '\n'
    )
    if (taskId && (kind === 'final' || kind === 'failed')) {
      await fs.writeFile(path.join(LOG_DIR, `${taskId}.json`), JSON.stringify(response, null, 2))
    }
  } catch {
    /* logging must never break a generation */
  }
}

// Standard-tier image-to-3D defaults (60 credits on Prism 3.1). texture_alignment
// "original_image" bakes the texture to match the concept's view.
const DEFAULT_BODY = {
  texture: true,
  pbr: true,
  texture_quality: 'standard',
  texture_alignment: 'original_image',
  orientation: 'align_image'
}

export function hasTripoKey() {
  return !!process.env['3D_AI_STUDIO_API_KEY']
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// 3D AI Studio rate limit = 3 requests/minute. Gate SUBMISSIONS so consecutive ones
// are spaced >= this interval (default 21s => <=3/min) no matter how fast a generation
// returns. All calls run through this one server process, so a module-level gate
// serializes them correctly. Override with TRIPO_MIN_SUBMIT_MS.
const MIN_SUBMIT_MS = Number(process.env.TRIPO_MIN_SUBMIT_MS) || 21000
let submitChain = Promise.resolve()
let lastSubmitAt = 0
function rateLimitGate() {
  const wait = submitChain.then(async () => {
    const since = Date.now() - lastSubmitAt
    if (lastSubmitAt && since < MIN_SUBMIT_MS) await sleep(MIN_SUBMIT_MS - since)
    lastSubmitAt = Date.now()
  })
  submitChain = wait.catch(() => {})
  return wait
}

function authHeaders() {
  const key = process.env['3D_AI_STUDIO_API_KEY']
  if (!key) throw new Error('3D_AI_STUDIO_API_KEY is not set — cannot generate 3D models')
  return { Authorization: `Bearer ${key}` }
}

/**
 * Submit an image-to-3D generation. Returns the task id.
 * @param {Buffer} imageBuffer PNG/JPEG bytes
 * @param {{ extraBody?: object }} [opts]
 * @returns {Promise<string>}
 */
export async function submitGeneration(imageBuffer, { extraBody = {} } = {}) {
  if (!imageBuffer?.length) throw new Error('submitGeneration requires image bytes')
  await rateLimitGate() // respect 3D AI Studio's 3-requests/minute cap
  const body = {
    ...DEFAULT_BODY,
    image: `data:image/png;base64,${imageBuffer.toString('base64')}`,
    ...extraBody
  }
  const res = await fetch(GENERATE_URL, {
    method: 'POST',
    headers: { ...authHeaders(), 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`Tripo submit ${res.status} ${res.statusText}: ${text.slice(0, 500)}`)
  }
  const json = await res.json()
  const taskId = json?.task_id || json?.id || json?.generation_request_id || json?.request_id
  await logResponse('submit', taskId ? String(taskId) : null, json)
  if (!taskId) throw new Error(`Tripo submit: no task id in response: ${JSON.stringify(json).slice(0, 300)}`)
  console.log(`[tripo] submitted task ${taskId}`)
  return String(taskId)
}

// Deep-scan an arbitrary response object for the GLB asset URL — the exact field /
// nesting varies by API version. Prefer a URL ending in .glb; fall back to the first
// asset-ish URL.
export function findGlbUrl(obj) {
  const seen = new Set()
  const queue = [obj]
  let fallback = null
  while (queue.length) {
    const cur = queue.shift()
    if (!cur || typeof cur !== 'object' || seen.has(cur)) continue
    seen.add(cur)
    for (const [k, v] of Object.entries(cur)) {
      if (typeof v === 'string') {
        if (/^https?:\/\/\S+\.glb(\?|#|$)/i.test(v)) return v
        if (!fallback && /^https?:\/\//.test(v) && /(asset|model|glb|download|output|file|url)/i.test(k)) {
          fallback = v
        }
      } else if (v && typeof v === 'object') {
        queue.push(v)
      }
    }
  }
  return fallback
}

/** Fetch the raw status payload for a task. */
export async function getStatus(taskId) {
  const res = await fetch(statusUrl(taskId), { headers: authHeaders() })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`Tripo status ${res.status} ${res.statusText}: ${text.slice(0, 300)}`)
  }
  return res.json()
}

/**
 * Poll a task until it reaches a terminal state. Returns the GLB asset URL.
 * @param {string} taskId
 * @param {{ intervalMs?: number, timeoutMs?: number, onProgress?: (p:object)=>void }} [opts]
 * @returns {Promise<string>}
 */
export async function waitForModel(taskId, { intervalMs = 5000, timeoutMs = 10 * 60 * 1000, onProgress } = {}) {
  const deadline = Date.now() + timeoutMs
  // Tripo sometimes flips to FINISHED a beat BEFORE the asset URL is written, so on
  // finished-but-empty we keep polling instead of failing.
  const MAX_EMPTY_FINISHED = 24 // x intervalMs ~= 2 min grace
  let emptyFinished = 0
  // eslint-disable-next-line no-constant-condition
  while (true) {
    const s = await getStatus(taskId)
    const status = String(s?.status || '').toUpperCase()
    onProgress?.({ status, progress: s?.progress })
    if (status === 'FINISHED' || status === 'COMPLETED' || status === 'SUCCESS' || status === 'DONE') {
      const url = findGlbUrl(s)
      if (url) {
        await logResponse('final', taskId, s)
        return url
      }
      emptyFinished++
      if (emptyFinished >= MAX_EMPTY_FINISHED) {
        await logResponse('final', taskId, s)
        console.error(`[tripo] task ${taskId} finished but no GLB URL after ${emptyFinished} polls.`)
        throw new Error(`Tripo finished but the asset URL never populated (task ${taskId}). Payload saved in output/_tripo.`)
      }
      onProgress?.({ status: 'FINISHED_AWAITING_ASSET', progress: 100 })
      if (Date.now() > deadline) throw new Error(`Tripo finished but asset URL not ready before timeout (task ${taskId})`)
      await sleep(intervalMs)
      continue
    }
    if (status === 'FAILED' || status === 'ERROR' || status === 'CANCELLED') {
      await logResponse('failed', taskId, s)
      throw new Error(`Tripo generation ${status}: ${s?.failure_reason || 'unknown reason'}`)
    }
    if (Date.now() > deadline) throw new Error(`Tripo generation timed out after ${timeoutMs}ms (last status ${status})`)
    await sleep(intervalMs)
  }
}

/** Download a finished GLB asset URL to a Buffer. */
export async function downloadAsset(url) {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Tripo asset download ${res.status} ${res.statusText}`)
  return Buffer.from(await res.arrayBuffer())
}

/**
 * Full image -> GLB: submit, poll, download. Returns raw GLB bytes.
 * @param {Buffer} imageBuffer
 * @param {{ onProgress?: (p:object)=>void, intervalMs?: number, timeoutMs?: number, extraBody?: object }} [opts]
 * @returns {Promise<Buffer>}
 */
export async function imageToModel(imageBuffer, opts = {}) {
  const taskId = await submitGeneration(imageBuffer, { extraBody: opts.extraBody })
  opts.onProgress?.({ status: 'SUBMITTED', taskId })
  const url = await waitForModel(taskId, opts)
  return downloadAsset(url)
}
