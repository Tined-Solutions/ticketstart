import { describe, it, expect } from 'vitest'
import { readFileSync, existsSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const srcDir = resolve(__dirname, '..', '..')

describe('CSS Migration Cleanup', () => {
  it('index.css stays lean after BEM purge and a11y additions', () => {
    const cssPath = resolve(srcDir, 'index.css')
    const content = readFileSync(cssPath, 'utf-8')
    const lineCount = content.split('\n').length

    // BEM purge got index.css under 200 lines. Intentional a11y/mobile CSS
    // (touch-action, prefers-reduced-motion, responsive admin tables) added
    // ~90 lines; the guard now tolerates that growth without allowing regrowth.
    expect(lineCount).toBeLessThanOrEqual(300)
  })

  it('App.css does not exist', () => {
    const appCssPath = resolve(srcDir, 'App.css')
    expect(existsSync(appCssPath)).toBe(false)
  })

  it('tokens.css exists and contains theme tokens', () => {
    const tokensPath = resolve(srcDir, 'tokens.css')
    const content = readFileSync(tokensPath, 'utf-8')

    expect(content).toContain('@theme inline')
    expect(content).toContain('--color-canvas')
    expect(content).toContain('data-theme')
  })
})
