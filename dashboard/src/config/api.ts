type FlowBBRuntimeConfig = {
  API_URL?: unknown
}

declare global {
  interface Window {
    __FLOWBB_CONFIG__?: FlowBBRuntimeConfig
  }
}

const runtimeApiUrl = typeof window.__FLOWBB_CONFIG__?.API_URL === 'string'
  ? window.__FLOWBB_CONFIG__.API_URL
  : undefined

export const apiBaseUrl = (
  runtimeApiUrl ?? import.meta.env.VITE_API_URL ?? 'http://localhost:8080'
).replace(/\/+$/, '')

