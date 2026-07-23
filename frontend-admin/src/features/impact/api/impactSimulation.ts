import type { ImpactReport, SimulateImpactRequest } from '@/features/impact/types';
import { customInstance } from '@/lib/axios';

export async function simulateImpact(
  body: SimulateImpactRequest,
  signal?: AbortSignal
): Promise<ImpactReport> {
  return customInstance<ImpactReport>({
    url: '/api/admin/impact-simulation/simulate',
    method: 'POST',
    data: body,
    signal,
  });
}
