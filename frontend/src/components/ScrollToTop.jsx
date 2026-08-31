import { useEffect, useLayoutEffect } from 'react'
import { useLocation } from 'react-router-dom'

/**
 * Scrolls the window to the top on every navigation, so each section starts at
 * its top instead of preserving the previous scroll position. Renders nothing.
 *
 * Why this shape (scroll-reset bugs fixed):
 * - `useLayoutEffect` (not `useEffect`) runs synchronously BEFORE the browser
 *   paints the new route, so the old scroll offset never flashes and nothing
 *   can repaint on top of the reset.
 * - The dependency is `location.key`, which changes on every real navigation.
 * - React Router does NOT change `location.key` when you navigate to the SAME
 *   path (e.g. clicking the navbar link for the page you are already on), so a
 *   capture-phase click handler on internal links covers that case too: any
 *   click on a same-origin `href="/..."` link scrolls to top immediately.
 * - `history.scrollRestoration = 'manual'` disables the browser's native
 *   restore-on-history-navigation, which otherwise fights this reset.
 */
function ScrollToTop() {
  const { key } = useLocation()

  useLayoutEffect(() => {
    if ('scrollRestoration' in window.history) {
      window.history.scrollRestoration = 'manual'
    }
    window.scrollTo(0, 0)
  }, [key])

  // Same-path link clicks (navbar re-click) do not change location.key, so the
  // effect above would not fire. Capture clicks on internal links and reset.
  useEffect(() => {
    const handleClick = (event) => {
      const target = event.target instanceof Element ? event.target : null
      const anchor = target?.closest?.('a[href]')
      const href = anchor?.getAttribute('href') ?? ''
      if (href.startsWith('/')) {
        window.scrollTo(0, 0)
      }
    }
    document.addEventListener('click', handleClick, { capture: true })
    return () => document.removeEventListener('click', handleClick, { capture: true })
  }, [])

  return null
}

export default ScrollToTop