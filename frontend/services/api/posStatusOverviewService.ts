import { apiClient } from './config';
import { normalizePosStatusOverview } from '../../utils/mapPosStatusOverview';
import type { PosStatusOverviewDto } from './posStatusOverviewTypes';

/** GET /api/pos/status/overview — combined license, register readiness, settings revision. */
export async function fetchPosStatusOverview(): Promise<PosStatusOverviewDto> {
  const raw = await apiClient.get<unknown>('/pos/status/overview');
  return normalizePosStatusOverview(raw);
}
