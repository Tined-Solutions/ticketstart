import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

/**
 * Test helpers for components that use TanStack Query hooks.
 *
 * Each render gets a fresh QueryClient so cached data never leaks between
 * tests. `retry: false` avoids the async retry backoff that would otherwise
 * delay error states and make `waitFor` time out.
 */
export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: Infinity,
        refetchOnWindowFocus: false,
      },
    },
  })
}

export function renderWithQueryClient(ui, client = createTestQueryClient()) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}
