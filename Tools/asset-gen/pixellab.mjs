// Pixellab API client (https://api.pixellab.ai/v2) — David's account, key in
// gitignored .env (PIXELLAB_API_KEY). Subscription: generations-based.
// Most generation endpoints are async: POST returns a job id, poll
// /background-jobs/{id} until done. Full spec: /v2/openapi.json.
//
// Usage: node --env-file=.env yourscript.mjs
//   import { pixellab, waitForJob, saveImages } from './pixellab.mjs'
//   const job = await pixellab('create-character-with-4-directions', {...})

const BASE = 'https://api.pixellab.ai/v2'

function key() {
  const k = process.env.PIXELLAB_API_KEY
  if (!k) throw new Error('PIXELLAB_API_KEY not set (Tools/asset-gen/.env)')
  return k
}

export async function pixellab(endpoint, body, method = 'POST') {
  const res = await fetch(`${BASE}/${endpoint}`, {
    method,
    headers: {
      Authorization: `Bearer ${key()}`,
      ...(body ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  let json
  try { json = JSON.parse(text) } catch { json = { raw: text } }
  if (!res.ok) throw new Error(`${endpoint} ${res.status}: ${text.slice(0, 400)}`)
  return json
}

export const balance = () => pixellab('balance', null, 'GET')

// Poll an async job until completion. Returns the finished job payload.
export async function waitForJob(jobId, { timeoutMs = 300000, intervalMs = 4000 } = {}) {
  const start = Date.now()
  while (true) {
    const job = await pixellab(`background-jobs/${jobId}`, null, 'GET')
    const status = job.status || job.state
    if (status === 'completed' || status === 'done' || status === 'succeeded') return job
    if (status === 'failed' || status === 'error') throw new Error('job failed: ' + JSON.stringify(job).slice(0, 400))
    if (Date.now() - start > timeoutMs) throw new Error('job timeout: ' + jobId)
    await new Promise(r => setTimeout(r, intervalMs))
  }
}

// Write any base64 images found in a response object to PNG files.
// Handles {images:[{base64}...]}, {image:{base64}}, and data-URL strings.
import { writeFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
export function saveImages(payload, pathPrefix) {
  const found = []
  const walk = (o) => {
    if (!o) return
    if (typeof o === 'string' && o.length > 200 && /^(data:image\/\w+;base64,)?[A-Za-z0-9+/=]+$/.test(o.slice(0, 300)))
      found.push(o.replace(/^data:image\/\w+;base64,/, ''))
    else if (Array.isArray(o)) o.forEach(walk)
    else if (typeof o === 'object') Object.values(o).forEach(walk)
  }
  walk(payload)
  mkdirSync(dirname(pathPrefix), { recursive: true })
  const files = []
  found.forEach((b64, i) => {
    const p = `${pathPrefix}${found.length > 1 ? '_' + i : ''}.png`
    writeFileSync(p, Buffer.from(b64, 'base64'))
    files.push(p)
  })
  return files
}
