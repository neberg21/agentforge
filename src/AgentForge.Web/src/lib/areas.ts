import { apiGet } from './http'

export type AreaInfo = { slug: string; title: string }

export function loadAreas(): Promise<AreaInfo[]> {
  return apiGet<AreaInfo[]>('/api/areas')
}
