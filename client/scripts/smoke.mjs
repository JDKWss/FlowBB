// Run with Vite and a disposable Chrome on --remote-debugging-port=9222:
// APP_URL=http://127.0.0.1:5173 node --experimental-websocket scripts/smoke.mjs
import assert from 'node:assert/strict'
import { writeFile } from 'node:fs/promises'

const appUrl = process.env.APP_URL ?? 'http://127.0.0.1:5173'

const pages = await fetch('http://127.0.0.1:9222/json').then(response => response.json())
const socket = new WebSocket(pages.find(page => page.type === 'page').webSocketDebuggerUrl)
await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }))
let sequence = 0
const pending = new Map()
const errors = []
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data)
  if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text)
  const callback = pending.get(message.id)
  if (callback) { pending.delete(message.id); callback(message) }
})
function command(method, params = {}) {
  const id = ++sequence
  return new Promise((resolve, reject) => {
    pending.set(id, message => message.error ? reject(new Error(message.error.message)) : resolve(message.result))
    socket.send(JSON.stringify({ id, method, params }))
  })
}
async function evaluate(expression) {
  const result = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails))
  return result.result.value
}
async function until(expression) {
  for (let attempt = 0; attempt < 100; attempt++) {
    if (await evaluate(expression)) return
    await new Promise(resolve => setTimeout(resolve, 100))
  }
  throw new Error(`Timed out: ${expression}`)
}
const visibleText = text => until(`document.body?.innerText.includes(${JSON.stringify(text)}) ?? false`)
async function click(label) {
  const expression = `[...document.querySelectorAll('button')].find(button => (button.getAttribute('aria-label') === ${JSON.stringify(label)} || button.innerText.trim() === ${JSON.stringify(label)}) && !button.disabled)`
  await until(`Boolean(${expression})`)
  await evaluate(`(${expression}).click()`)
}
async function viewport(width) {
  await command('Emulation.setDeviceMetricsOverride', { width, height: 844, deviceScaleFactor: 1, mobile: true })
  assert.equal(await evaluate('document.documentElement.scrollWidth <= window.innerWidth'), true, `Overflow at ${width}px`)
}
try {
  await command('Runtime.enable')
  await viewport(390)
  await command('Page.navigate', { url: appUrl })
  await visibleText('Koncert na Rynku')
  const validation = await evaluate(`(async () => {
    const data = await import('/src/mocks/data.ts')
    const schemas = await import('/src/types/validation.ts')
    schemas.eventDetailsSchema.array().parse(data.eventDetails)
    schemas.eventSummarySchema.array().parse(data.events)
    schemas.groupSchema.array().parse(data.groups)
    Object.values(data.routes).forEach(route => schemas.routeSchema.parse(route))
    return data.events.length
  })()`)
  assert.equal(validation, 4)
  await click('Open event: Koncert na Rynku')
  await visibleText('How can you get there?')
  assert.equal(await evaluate(`history.state?.screen`), 'details')
  await evaluate(`history.back()`)
  await visibleText('Your next plan.')
  assert.equal(await evaluate(`history.state?.screen`), 'events')
  await evaluate(`history.forward()`)
  await visibleText('How can you get there?')
  assert.equal(await evaluate(`history.state?.screen`), 'details')
  await click('Plan my trip\nChoose how you\'ll get there')
  await click("I'm going")
  await visibleText('83 people')
  await click('See my route')
  await visibleText('18:12')
  await visibleText('21:44')
  await click('Find your crew')
  await visibleText('4/6')
  await click('Join Nowi w Bielsku')
  await visibleText('5/6')
  await visibleText('Your place is confirmed')
  for (const width of [320, 390, 480, 1024]) await viewport(width)
  await viewport(390)
  const screenshot = await command('Page.captureScreenshot', { format: 'png' })
  await writeFile('/tmp/flowbb-crew-verified.png', Buffer.from(screenshot.data, 'base64'))
  await click('Leave Nowi w Bielsku')
  await visibleText('4/6')
  assert.equal(await evaluate(`[...document.querySelectorAll('button')].find(b => b.innerText === 'Group full')?.disabled`), true)
  await click('Back to your route')
  await click('Back to transport selection')
  await click('Back to event details')
  await click('Plan my trip\nChoose how you\'ll get there')
  await click("I'm going")
  await visibleText('83 people')
  await click('Back to event details')
  await click('Back to events')
  await click('Open event: Nocny Bieg na Błoniach')
  await click('Plan my trip\nChoose how you\'ll get there')
  await click("I'm going")
  await click('See my route')
  await visibleText('Limited return connection')
  assert.deepEqual(errors, [])
  console.log('PASS: schemas, browser back/forward, attendance idempotency, route, return gap, join/leave/full crew, responsive widths, no runtime errors.')
} finally {
  socket.close()
}
