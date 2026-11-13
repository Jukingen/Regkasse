import { customInstance } from '@/lib/axios';

import type {
  CreateTseIncidentRequest,
  TseIncident,
  TseIncidentDashboard,
  TseIncidentReport,
  UpdateTseIncidentStatusRequest,
} from '../types';

export async function getTseIncidentDashboard(
  tenantId?: string,
  signal?: AbortSignal
): Promise<TseIncidentDashboard> {
  return customInstance<TseIncidentDashboard>({
    url: '/api/admin/tse/incidents/dashboard',
    method: 'GET',
    params: tenantId ? { tenantId } : undefined,
    signal,
  });
}

export async function listTseIncidents(
  tenantId?: string,
  days = 30,
  signal?: AbortSignal
): Promise<TseIncident[]> {
  const toUtc = new Date();
  const fromUtc = new Date(toUtc.getTime() - days * 24 * 60 * 60 * 1000);
  return customInstance<TseIncident[]>({
    url: '/api/admin/tse/incidents',
    method: 'GET',
    params: {
      ...(tenantId ? { tenantId } : {}),
      fromUtc: fromUtc.toISOString(),
      toUtc: toUtc.toISOString(),
    },
    signal,
  });
}

export async function createTseIncident(
  body: CreateTseIncidentRequest,
  signal?: AbortSignal
): Promise<TseIncident> {
  return customInstance<TseIncident>({
    url: '/api/admin/tse/incidents',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function getTseIncident(
  incidentId: string,
  signal?: AbortSignal
): Promise<TseIncident> {
  return customInstance<TseIncident>({
    url: `/api/admin/tse/incidents/${incidentId}`,
    method: 'GET',
    signal,
  });
}

export async function updateTseIncidentStatus(
  incidentId: string,
  body: UpdateTseIncidentStatusRequest,
  signal?: AbortSignal
): Promise<TseIncident> {
  return customInstance<TseIncident>({
    url: `/api/admin/tse/incidents/${incidentId}/status`,
    method: 'POST',
    data: body,
    signal,
  });
}

export async function getTseIncidentReport(
  incidentId: string,
  signal?: AbortSignal
): Promise<TseIncidentReport> {
  return customInstance<TseIncidentReport>({
    url: `/api/admin/tse/incidents/${incidentId}/report`,
    method: 'GET',
    signal,
  });
}
