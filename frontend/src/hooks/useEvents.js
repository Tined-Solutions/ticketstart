import { useQuery } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'

export function useEvents() {
  return useQuery({
    queryKey: queryKeys.events,
    queryFn: async () => {
      const response = await apiClient.get('/events')
      return response.data
    },
    staleTime: 60_000,
  })
}
