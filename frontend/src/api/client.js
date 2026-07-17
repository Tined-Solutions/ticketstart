import axios from 'axios'

const baseURL =
  import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.DEV ? '/api' : 'http://localhost:5193')

const apiClient = axios.create({
  baseURL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config) => {
  const mutatingMethods = ['post', 'put', 'patch', 'delete']
  if (mutatingMethods.includes(config.method?.toLowerCase())) {
    config.headers['X-CSRF-PROTECT'] = '1'
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Don't redirect for /auth/me and /auth/logout — these are expected
    // to return 401 for unauthenticated users (the AuthProvider handles it)
    // and redirecting here would create an infinite reload loop.
    const isAuthEndpoint =
      error.config?.url && (
        error.config.url.endsWith('/auth/me') ||
        error.config.url.endsWith('/auth/logout')
      )

    if (error.response?.status === 401 && !isAuthEndpoint) {
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default apiClient
