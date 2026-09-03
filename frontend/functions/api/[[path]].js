// Cloudflare Pages Function — same-origin API proxy.
//
// The browser calls /api/* on the Pages domain; this function forwards the
// request to the Render backend, preserving method, headers (cookies, CSRF)
// and body, then returns the upstream response — including Set-Cookie.
//
// Why a Function instead of a `_redirects` entry: Cloudflare Pages `_redirects`
// proxying (status 200) only supports relative URLs — it cannot proxy to
// external domains. A Pages Function is the supported way to keep the
// same-origin topology (no CORS, cookies stay SameSite=Lax).

const DEFAULT_API_BACKEND = 'https://api-staging.onrender.com'

export async function onRequest(context) {
  const { request, env } = context
  const url = new URL(request.url)

  const backend = env.API_BACKEND || DEFAULT_API_BACKEND
  const target = `${backend}${url.pathname}${url.search}`

  const headers = new Headers(request.headers)
  headers.set('Host', new URL(backend).host)

  const init = {
    method: request.method,
    headers,
    redirect: 'manual',
  }
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    init.body = request.body
  }

  try {
    const upstream = await fetch(target, init)
    const responseHeaders = new Headers(upstream.headers)
    // Strip hop-by-hop headers the edge manages itself.
    responseHeaders.delete('connection')
    responseHeaders.delete('keep-alive')
    responseHeaders.delete('transfer-encoding')
    return new Response(upstream.body, {
      status: upstream.status,
      statusText: upstream.statusText,
      headers: responseHeaders,
    })
  } catch {
    return Response.json(
      {
        error: {
          message:
            'El servicio de la API no está disponible en este momento. Intentá de nuevo en unos minutos.',
        },
      },
      { status: 502 }
    )
  }
}