import { useQuery } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'

export function useManagementEvents() {
  return useQuery({
    queryKey: queryKeys.managementEvents,
    queryFn: async () => {
      const response = await apiClient.get('/events/manage')
      return response.data
    },
    staleTime: 60_000,
  })
}
