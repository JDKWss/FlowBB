import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <main>
      <h1>FlowBB Client</h1>
    </main>
  </StrictMode>,
)
