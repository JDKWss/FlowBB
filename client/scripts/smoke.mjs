// Run with Vite and a disposable Chrome on --remote-debugging-port=9222:
// APP_URL=http://127.0.0.1:5173 node --experimental-websocket scripts/smoke.mjs
import assert from 'node:assert/strict'
import { writeFile } from 'node:fs/promises'

const appUrl = process.env.APP_URL ?? 'http://127.0.0.1:5173'
const chromeDebugUrl = process.env.CHROME_DEBUG_URL ?? 'http://127.0.0.1:9222'
const useRemoteMapStyle = process.env.USE_REMOTE_MAP_STYLE === 'true'
const routeMode = process.env.ROUTE_MODE ?? 'Walking'
const routeNavigationCycles = Number(process.env.ROUTE_NAVIGATION_CYCLES ?? '1')
if (!Number.isInteger(routeNavigationCycles) || routeNavigationCycles < 1) {
  throw new Error(`ROUTE_NAVIGATION_CYCLES must be a positive integer: ${routeNavigationCycles}`)
}
const routeModeButtons = {
  Walking: 'Select Walk',
  Bike: 'Select Bike',
  Car: 'Select Car',
}
if (!(routeMode in routeModeButtons)) {
  throw new Error(`Unsupported ROUTE_MODE: ${routeMode}`)
}

const pages = await fetch(`${chromeDebugUrl}/json`).then(response => response.json())
const socket = new WebSocket(pages.find(page => page.type === 'page').webSocketDebuggerUrl)
await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }))
let sequence = 0
const pending = new Map()
const errors = []
const smokeMapStyle = Buffer.from(JSON.stringify({
  version: 8,
  sources: {},
  layers: [{ id: 'background', type: 'background', paint: { 'background-color': '#d4d4d4' } }],
})).toString('base64')
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data)
  if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text)
  if (message.method === 'Fetch.requestPaused') {
    void command('Fetch.fulfillRequest', {
      requestId: message.params.requestId,
      responseCode: 200,
      responseHeaders: [{ name: 'Content-Type', value: 'application/json' }],
      body: smokeMapStyle,
    })
  }
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
  if (!useRemoteMapStyle) {
    await command('Fetch.enable', {
      patterns: [{ urlPattern: 'https://tiles.openfreemap.org/styles/liberty*', requestStage: 'Request' }],
    })
  }
  await viewport(390)
  await command('Page.navigate', { url: `${appUrl}?smoke=${Date.now()}` })
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
  await click(routeModeButtons[routeMode])
  await click("I'm going")
  await visibleText('83 people')
  await click('See my route')
  await visibleText('DEMO ROUTE')
  await visibleText(routeMode)
  assert.equal(await evaluate(`Boolean(document.querySelector('[data-testid="route-map"]'))`), true)
  assert.equal(await evaluate(`Boolean(document.querySelector('[data-testid="route-fullscreen-toggle"]'))`), true)
  await until(`Boolean(document.querySelector('[data-testid="route-start-marker"]'))`)
  await until(`Boolean(document.querySelector('[data-testid="route-destination-marker"]'))`)
  for (let cycle = 1; cycle <= routeNavigationCycles; cycle++) {
    await until(`Boolean(document.querySelector('[data-testid="route-map-unavailable"]')) || !document.querySelector('[data-testid="route-map-loading"]')`)
    assert.equal(
      await evaluate(`Boolean(document.querySelector('[data-testid="route-map-unavailable"]'))`),
      false,
      `Route map unavailable on cycle ${cycle}`,
    )
    const dimensions = await evaluate(`(() => {
      const container = document.querySelector('[data-testid="route-map"]')
      const canvas = container?.querySelector('.maplibregl-canvas')
      const containerRect = container?.getBoundingClientRect()
      const canvasRect = canvas?.getBoundingClientRect()
      return {
        containerWidth: containerRect?.width ?? 0,
        containerHeight: containerRect?.height ?? 0,
        canvasWidth: canvasRect?.width ?? 0,
        canvasHeight: canvasRect?.height ?? 0,
      }
    })()`)
    assert.ok(
      dimensions.containerWidth > 0 && dimensions.containerHeight > 0,
      `Route map container collapsed on cycle ${cycle}: ${JSON.stringify(dimensions)}`,
    )
    assert.ok(
      dimensions.canvasWidth > 0 && dimensions.canvasHeight > 0,
      `Route map canvas collapsed on cycle ${cycle}: ${JSON.stringify(dimensions)}`,
    )
    if (cycle < routeNavigationCycles) {
      await click('Back to transport selection')
      await click('See my route')
      await visibleText('DEMO ROUTE')
    }
  }
  await command('Runtime.evaluate', {
    expression: `document.querySelector('[data-testid="route-fullscreen-toggle"]').click()`,
    userGesture: true,
  })
  await until(`document.fullscreenElement?.getAttribute('data-testid') === 'route-map'`)
  await visibleText(routeMode)
  assert.equal(await evaluate(`document.querySelector('[data-testid="route-fullscreen-toggle"]')?.getAttribute('aria-label')`), 'Exit full screen')
  await command('Runtime.evaluate', {
    expression: 'document.exitFullscreen()',
    awaitPromise: true,
    userGesture: true,
  })
  await until(`document.fullscreenElement === null`)
  if (useRemoteMapStyle) {
    await new Promise(resolve => setTimeout(resolve, 3_000))
  }
  const routeScreenshot = await command('Page.captureScreenshot', { format: 'png' })
  await writeFile(
    `/tmp/flowbb-route-${routeMode.toLowerCase()}-verified.png`,
    Buffer.from(routeScreenshot.data, 'base64'),
  )
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
  assert.equal(await evaluate(`Boolean(document.querySelector('[data-testid="route-map-section"]'))`), false)
  assert.deepEqual(errors, [])
  console.log('PASS: schemas, browser back/forward, attendance idempotency, route, return gap, join/leave/full crew, responsive widths, no runtime errors.')
} finally {
  socket.close()
}
