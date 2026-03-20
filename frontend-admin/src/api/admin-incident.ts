/**
 * Admin incident investigation — authoritative aggregate (replay batch + audit + FO per payment).
 * Primary consumer: `/rksv/incident`. Do not use from POS.
 */
import { customInstance } from '@/lib/axios';
import type { ReplayBatchDetailResponse } from '@/api/replay-batch';
import type { FinanzOnlineReconciliationItemDto } from '@/api/finanzonline-reconciliation';

export interface IncidentInvestigationHints {
  hasLockTimeoutAudit: boolean;
  hasPayloadImmutableMismatchAudit: boolean;
  finanzOnlineSubmittedCount: number;
  finanzOnlineOpenOrProblemCount: number;
}

/** Mirrors backend AuditLogEntryDto (camelCase JSON). */
export interface IncidentAuditLogEntry {
  id?: string;
  timestamp: string;
  action: string;
  status: string | number;
  entityType?: string;
  description?: string | null;
  requestData?: string | null;
  responseData?: string | null;
}

export interface IncidentInvestigationResponse {
  replayBatch: ReplayBatchDetailResponse;
  auditLogs: IncidentAuditLogEntry[];
  finanzOnlineReconciliation: FinanzOnlineReconciliationItemDto[];
  hints: IncidentInvestigationHints;
}

export async function getIncidentInvestigation(correlationId: string): Promise<IncidentInvestigationResponse> {
  return customInstance<IncidentInvestigationResponse>({
    url: `/api/admin/incidents/${encodeURIComponent(correlationId)}`,
    method: 'GET',
  });
}
