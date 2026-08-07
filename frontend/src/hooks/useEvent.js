import { useQuery } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'

export function useEvent(id) {
  return useQuery({
    queryKey: queryKeys.event(id),
    queryFn: async () => {
      const response = await apiClient.get(`/events/${id}`)
      return response.data
    },
    staleTime: 60_000,
    enabled: Boolean(id),
  })
}
