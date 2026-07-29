import { customInstance } from '@/lib/axios';

import type {
  RksvAusfallEpisode,
  RksvAusfallTriggerRequest,
  RksvAusfallTriggerResponse,
} from '../types';

function str(v: unknown): string {
  return v == null ? '' : String(v);
}

function strOrNull(v: unknown): string | null {
  if (v == null || v === '') return null;
  return String(v);
}

export function normalizeRksvAusfallEpisode(raw: unknown): RksvAusfallEpisode | null {
  if (!raw || typeof raw !== 'object') return null;
  const b = raw as Record<string, unknown>;
  const id = str(b.id ?? b.Id);
  if (!id) return null;
  return {
    id,
    tenantId: str(b.tenantId ?? b.TenantId),
    deviceId: strOrNull(b.deviceId ?? b.DeviceId),
    deviceSerial: strOrNull(b.deviceSerial ?? b.DeviceSerial),
    episodeType: str(b.episodeType ?? b.EpisodeType),
    operationKind: str(b.operationKind ?? b.OperationKind),
    begruendung: str(b.begruendung ?? b.Begruendung),
    beginnUtc: strOrNull(b.beginnUtc ?? b.BeginnUtc),
    endeUtc: strOrNull(b.endeUtc ?? b.EndeUtc),
    status: str(b.status ?? b.Status),
    outboxMessageId: strOrNull(b.outboxMessageId ?? b.OutboxMessageId),
    externalReference: strOrNull(b.externalReference ?? b.ExternalReference),
    certificateSerial: strOrNull(b.certificateSerial ?? b.CertificateSerial),
    kassenId: strOrNull(b.kassenId ?? b.KassenId),
    cashRegisterId: strOrNull(b.cashRegisterId ?? b.CashRegisterId),
    relatedAusfallEpisodeId: strOrNull(b.relatedAusfallEpisodeId ?? b.RelatedAusfallEpisodeId),
    operatorNote: strOrNull(b.operatorNote ?? b.OperatorNote),
    createdBy: strOrNull(b.createdBy ?? b.CreatedBy),
    approvedBy: strOrNull(b.approvedBy ?? b.ApprovedBy),
    approvedAtUtc: strOrNull(b.approvedAtUtc ?? b.ApprovedAtUtc),
    lastErrorCode: strOrNull(b.lastErrorCode ?? b.LastErrorCode),
    lastErrorMessage: strOrNull(b.lastErrorMessage ?? b.LastErrorMessage),
    createdAtUtc: str(b.createdAtUtc ?? b.CreatedAtUtc),
    updatedAtUtc: strOrNull(b.updatedAtUtc ?? b.UpdatedAtUtc),
  };
}

export async function listRksvAusfallEpisodes(params?: {
  status?: string;
  signal?: AbortSignal;
}): Promise<RksvAusfallEpisode[]> {
  const data = await customInstance<unknown>({
    url: '/api/admin/tse/ausfall/episodes',
    method: 'GET',
    params: params?.status ? { status: params.status } : undefined,
    signal: params?.signal,
  });
  const list = Array.isArray(data) ? data : [];
  return list.map(normalizeRksvAusfallEpisode).filter((e): e is RksvAusfallEpisode => e != null);
}

export async function listAusfallBegruendungCodes(signal?: AbortSignal): Promise<string[]> {
  const data = await customInstance<unknown>({
    url: '/api/admin/tse/ausfall/begruendung-codes',
    method: 'GET',
    signal,
  });
  return Array.isArray(data) ? data.map(String) : [];
}

export async function triggerRksvAusfall(
  body: RksvAusfallTriggerRequest
): Promise<RksvAusfallTriggerResponse> {
  return customInstance<RksvAusfallTriggerResponse>({
    url: '/api/admin/tse/ausfall/trigger',
    method: 'POST',
    data: body,
  });
}

export async function approveRksvAusfall(
  id: string,
  operatorNote?: string
): Promise<RksvAusfallTriggerResponse> {
  return customInstance<RksvAusfallTriggerResponse>({
    url: `/api/admin/tse/ausfall/episodes/${id}/approve`,
    method: 'POST',
    data: { operatorNote: operatorNote ?? null },
  });
}

export async function markManualRksvAusfall(
  id: string,
  opts?: { operatorNote?: string; externalReference?: string }
): Promise<RksvAusfallTriggerResponse> {
  return customInstance<RksvAusfallTriggerResponse>({
    url: `/api/admin/tse/ausfall/episodes/${id}/mark-manual`,
    method: 'POST',
    data: {
      operatorNote: opts?.operatorNote ?? null,
      externalReference: opts?.externalReference ?? null,
    },
  });
}

export async function cancelRksvAusfall(id: string): Promise<RksvAusfallTriggerResponse> {
  return customInstance<RksvAusfallTriggerResponse>({
    url: `/api/admin/tse/ausfall/episodes/${id}/cancel`,
    method: 'POST',
  });
}
