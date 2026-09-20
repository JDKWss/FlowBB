// Real E2E against Vite + FlowBB ASP.NET + Neo4j + private FastAPI.
// Start Chrome with remote debugging, then run:
// APP_URL=http://127.0.0.1:5174 node --experimental-websocket scripts/e2e-real.mjs
import assert from 'node:assert/strict'

const appUrl = process.env.APP_URL ?? 'http://127.0.0.1:5173'
const chromeDebugUrl = process.env.CHROME_DEBUG_URL ?? 'http://127.0.0.1:9222'
const navigationCycles = Number(process.env.ROUTE_NAVIGATION_CYCLES ?? '20')

const pages = await fetch(`${chromeDebugUrl}/json`).then(response => response.json())
const page = pages.find(candidate => candidate.type === 'page')
assert.ok(page, 'No debuggable Chrome page was found.')
const socket = new WebSocket(page.webSocketDebuggerUrl)
await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }))

let sequence = 0
const pending = new Map()
const browserErrors = []
const requests = []
const responses = new Map()

socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data)
  if (message.method === 'Runtime.exceptionThrown') browserErrors.push(message.params.exceptionDetails.text)
  if (message.method === 'Network.requestWillBeSent') {
    requests.push({
      requestId: message.params.requestId,
      method: message.params.request.method,
      url: message.params.request.url,
    })
  }
  if (message.method === 'Network.responseReceived') {
    responses.set(message.params.requestId, {
      status: message.params.response.status,
      url: message.params.response.url,
    })
  }
  const callback = pending.get(message.id)
  if (callback) {
    pending.delete(message.id)
    callback(message)
  }
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

async function until(expression, message = expression) {
  for (let attempt = 0; attempt < 200; attempt++) {
    if (await evaluate(expression)) return
    await new Promise(resolve => setTimeout(resolve, 100))
  }
  throw new Error(`Timed out: ${message}`)
}

const visibleText = text => until(
  `document.body?.innerText.includes(${JSON.stringify(text)}) ?? false`,
  `visible text ${JSON.stringify(text)}`,
)

async function click(label) {
  const button = `[...document.querySelectorAll('button')].find(item => item.getAttribute('aria-label') === ${JSON.stringify(label)} && !item.disabled)`
  await until(`Boolean(${button})`, `enabled button ${JSON.stringify(label)}`)
  await evaluate(`(${button}).click()`)
}

async function clickText(text) {
  const button = `[...document.querySelectorAll('button')].find(item => item.innerText.includes(${JSON.stringify(text)}) && !item.disabled)`
  await until(`Boolean(${button})`, `enabled button containing ${JSON.stringify(text)}`)
  await evaluate(`(${button}).click()`)
}

function apiRequest(method, pathPart) {
  return requests.find(request => request.method === method && request.url.includes(pathPart))
}

async function responseJson(request) {
  await until(`performance.getEntriesByName(${JSON.stringify(request.url)}).some(entry => entry.responseEnd > 0)`)
  const body = await command('Network.getResponseBody', { requestId: request.requestId })
  return JSON.parse(body.body)
}

try {
  await command('Runtime.enable')
  await command('Network.enable')
  await command('Emulation.setDeviceMetricsOverride', {
    width: 390,
    height: 844,
    deviceScaleFactor: 1,
    mobile: true,
  })
  await command('Page.navigate', { url: `${appUrl}?e2e=${Date.now()}` })

  await visibleText('Koncert na Rynku')
  await click('Open event: Koncert na Rynku')
  await visibleText('How can you get there?')
  await clickText('Continue')
  await visibleText('How will you get there?')
  await click('Select Walk')
  await clickText("I'm going")
  await visibleText('83 people are joining this event.')

  const attendanceRequest = apiRequest('POST', '/attendance')
  assert.ok(attendanceRequest, 'Browser did not POST Attendance to ASP.NET.')
  const attendance = await responseJson(attendanceRequest)
  assert.equal(responses.get(attendanceRequest.requestId)?.status, 200)
  assert.equal(attendance.isNew, true)
  assert.equal(attendance.participantsCount, 83)
  assert.equal(attendance.transportMode, 'Walking')

  await clickText('See my route')
  await visibleText('Road routing')
  await until(`Boolean(document.querySelector('[data-testid="route-map"]'))`)
  await until(`!document.querySelector('[data-testid="route-map-loading"]')`)

  const routeRequest = apiRequest('GET', '/route?userId=dddddddd-dddd-dddd-dddd-dddddddddddd')
  assert.ok(routeRequest, 'Browser did not GET the route from ASP.NET.')
  const route = await responseJson(routeRequest)
  assert.equal(route.plannerSource, 'RoadRouting')
  assert.ok(!route.outbound.stops, 'A road route must not carry bus stops.')
  assert.ok(route.outbound.distanceMeters > 0)
  assert.equal(route.outbound.geometry.type, 'LineString')
  assert.ok(route.outbound.geometry.coordinates.length > 2)
  assert.equal(
    await evaluate(`document.body.innerText.includes(${JSON.stringify(`${(route.outbound.distanceMeters / 1000).toFixed(1)} km`)})`),
    true,
  )

  for (let cycle = 1; cycle <= navigationCycles; cycle++) {
    const dimensions = await evaluate(`(() => {
      const container = document.querySelector('[data-testid="route-map"]')
      const canvas = container?.querySelector('.maplibregl-canvas')
      const containerRect = container?.getBoundingClientRect()
      const canvasRect = canvas?.getBoundingClientRect()
      return [containerRect?.width ?? 0, containerRect?.height ?? 0, canvasRect?.width ?? 0, canvasRect?.height ?? 0]
    })()`)
    assert.ok(dimensions.every(value => value > 0), `Collapsed map on navigation ${cycle}: ${dimensions}`)
    assert.equal(await evaluate(`Boolean(document.querySelector('[data-testid="route-map-unavailable"]'))`), false)
    if (cycle < navigationCycles) {
      await click('Back to transport selection')
      await clickText('See my route')
      await until(`Boolean(document.querySelector('[data-testid="route-map"]'))`)
      await until(`!document.querySelector('[data-testid="route-map-loading"]')`)
    }
  }

  await clickText('Find your crew')
  await visibleText('4/6')
  await click('Join Nowi w Bielsku')
  await visibleText('5/6')
  await visibleText('Your place is confirmed')
  await click('Leave Nowi w Bielsku')
  await visibleText('4/6')

  for (const [method, path] of [
    ['GET', '/api/events'],
    ['GET', '/api/events/11111111-1111-1111-1111-111111111111'],
    ['POST', '/attendance'],
    ['GET', '/route?userId=dddddddd-dddd-dddd-dddd-dddddddddddd'],
    ['GET', '/groups?userId=dddddddd-dddd-dddd-dddd-dddddddddddd'],
    ['POST', '/api/groups/22222222-2222-2222-2222-222222222222/members'],
    ['DELETE', '/api/groups/22222222-2222-2222-2222-222222222222/members/dddddddd-dddd-dddd-dddd-dddddddddddd'],
  ]) {
    assert.ok(apiRequest(method, path), `Missing browser request ${method} ${path}`)
  }

  const forbidden = requests.filter(request => request.url.includes('localhost:8000') || request.url.includes('routing:8000'))
  assert.deepEqual(forbidden, [], 'The browser called private FastAPI directly.')
  assert.deepEqual(browserErrors, [], `Browser runtime errors: ${browserErrors.join(', ')}`)

  console.log(JSON.stringify({
    result: 'PASS',
    navigationCycles,
    attendance: { isNew: attendance.isNew, participantsCount: attendance.participantsCount },
    route: {
      plannerSource: route.plannerSource,
      distanceMeters: route.outbound.distanceMeters,
      geometryPointCount: route.outbound.geometry.coordinates.length,
    },
    apiRequests: requests
      .filter(request => request.url.startsWith('http://localhost:8080/'))
      .map(request => `${request.method} ${request.url}`),
    directFastApiRequests: forbidden.length,
  }, null, 2))
} finally {
  socket.close()
}
