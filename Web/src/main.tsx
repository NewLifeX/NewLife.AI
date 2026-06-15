import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@/i18n'
// 自托管 Google Material Icons / Symbols 字体（替代 CDN）
import 'material-icons/iconfont/filled.css'
import 'material-icons/iconfont/outlined.css'
import 'material-symbols/outlined.css'
import '@/styles/index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
