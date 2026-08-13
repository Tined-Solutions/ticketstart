import { useQuery } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'

export function useManagementEvent(id) {
  return useQuery({
    queryKey: queryKeys.managementEvent(id),
    queryFn: async () => {
      const response = await apiClient.get(`/events/${id}/manage`)
      return response.data
    },
    staleTime: 60_000,
    enabled: Boolean(id),
  })
}
