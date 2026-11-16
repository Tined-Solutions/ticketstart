import { useQuery } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'

/**
 * Public checkout settings served by the backend (GET /api/config/checkout).
 * Currently only `serviceFeePercentage`. Rarely changes, so it is cached
 * for the whole session.
 */
export function useCheckoutConfig() {
  return useQuery({
    queryKey: queryKeys.checkoutConfig,
    queryFn: async () => {
      const response = await apiClient.get('/config/checkout')
      return response.data
    },
    staleTime: Infinity,
  })
}
