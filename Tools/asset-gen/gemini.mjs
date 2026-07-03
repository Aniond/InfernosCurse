// Google Gemini image generation ("Nano Banana" = gemini-2.5-flash-image). Produces a
// more realistic painted look than gpt-image-1 for concept work. Returns raw PNG bytes.
//
// Key: process.env.GEMINI_API_KEY (or GOOGLE_API_KEY). Get one at
// https://aistudio.google.com/apikey.

const MODEL = process.env.GEMINI_IMAGE_MODEL || 'gemini-2.5-flash-image'
const endpoint = (key) =>
  `https://generativelanguage.googleapis.com/v1beta/models/${MODEL}:generateContent?key=${encodeURIComponent(key)}`

export function geminiKey() {
  return process.env.GEMINI_API_KEY || process.env.GOOGLE_API_KEY || ''
}
export function hasGeminiKey() {
  return !!geminiKey()
}

/**
 * Generate one concept image for a prompt via Gemini. Returns PNG (or whatever inline
 * image mime the model returns) bytes. Optional reference images are passed as inline
 * input so the model uses them as the visual basis.
 * @param {string} prompt
 * @param {{ referenceImages?: Buffer[] }} [opts]
 * @returns {Promise<Buffer>}
 */
export async function generateImage(prompt, { referenceImages = [] } = {}) {
  const key = geminiKey()
  if (!key) throw new Error('GEMINI_API_KEY (or GOOGLE_API_KEY) is not set — cannot generate Gemini images')
  if (!prompt || !prompt.trim()) throw new Error('generateImage requires a non-empty prompt')

  // Reference images first, then the text — the model conditions the generation on them.
  const parts = []
  for (const ref of referenceImages) {
    if (ref?.length) parts.push({ inlineData: { mimeType: 'image/png', data: ref.toString('base64') } })
  }
  parts.push({ text: prompt })

  const res = await fetch(endpoint(key), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      contents: [{ parts }],
      // Image models reject IMAGE-only output — they return TEXT + IMAGE; we pick the
      // image part below.
      generationConfig: { responseModalities: ['TEXT', 'IMAGE'] }
    })
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`Gemini image ${res.status} ${res.statusText}: ${text.slice(0, 500)}`)
  }
  const json = await res.json()
  const outParts = json?.candidates?.[0]?.content?.parts || []
  const img = outParts.find((p) => p?.inlineData?.data)
  if (!img) {
    const reason = json?.candidates?.[0]?.finishReason || json?.promptFeedback?.blockReason
    throw new Error(`Gemini returned no image${reason ? ` (${reason})` : ''}: ${JSON.stringify(json).slice(0, 300)}`)
  }
  return Buffer.from(img.inlineData.data, 'base64')
}
