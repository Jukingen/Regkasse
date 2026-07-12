import { getApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { GetApiAdminFiscalExportParams } from '@/api/generated/model';
import type {
  RksvComplianceReport,
  RksvComplianceReportQueryParams,
} from '@/features/rksv/compliance/types';
import { normalizeRksvBackendEnvironment } from '@/features/rksv/normalizeRksvBackendEnvironment';
import type { RksvBackendEnvironmentStatus } from '@/features/rksv/types/rksvBackendEnvironment';
import {
  normalizeCashRegisterListBody,
  type CashRegisterRow,
} from '@/features/tagesabschluss/normalizers';
import { AXIOS_INSTANCE } from '@/lib/axios';

/** GET /api/rksv/status — canonical RKSV environment status for POS and Admin. */
export async function getRksvBackendEnvironment(signal?: AbortSignal): Promise<RksvBackendEnvironmentStatus> {
  const response = await AXIOS_INSTANCE.get<unknown>('/api/rksv/status', { signal });
  const normalized = normalizeRksvBackendEnvironment(response.data);
  if (!normalized) {
    throw new Error('RKSV environment response was empty or invalid');
  }
  return normalized;
}

/** Must match backend FiscalExportDisclaimerHeaders — export requests fail without acknowledgment when required. */
export const FISCAL_EXPORT_DISCLAIMER_ACK_HEADER = 'X-Disclaimer-Acknowledged';
export const FISCAL_EXPORT_DISCLAIMER_ACK_VALUE = 'true';

export function fiscalExportDisclaimerAckHeaders(): Record<string, string> {
  return { [FISCAL_EXPORT_DISCLAIMER_ACK_HEADER]: FISCAL_EXPORT_DISCLAIMER_ACK_VALUE };
}

/**
 * Manual wrapper governance:
 * - Only keep adapters here for non-standard transport needs (blob download, custom query format)
 *   or response-shape normalization not expressible in generated clients.
 * - If an endpoint is available in `@/api/generated/*`, call it directly from the page/hook layer.
 * - Every new manual wrapper must include a short WHY comment and a removal condition.
 */
export async function getAdminCashRegisters(): Promise<CashRegisterRow[]> {
  const response = await getApiCashRegister();
  return normalizeCashRegisterListBody(response);
}

export function downloadOfflinePayloadHashExportCsv(params: {
  maxRows?: number;
  cashRegisterId?: string;
}) {
  return AXIOS_INSTANCE.get<Blob>('/api/admin/offline-payload-hash/export', {
    params,
    responseType: 'blob',
  }).then((response) => response.data);
}

/**
 * Normalizes fiscal export JSON envelope `{ legalNotice, exports: [ package ] }` to the inner package for UI preview.
 */
export function unwrapFiscalExportEnvelope(data: unknown): unknown {
  if (
    data !== null &&
    typeof data === 'object' &&
    'exports' in data &&
    Array.isArray((data as { exports: unknown }).exports)
  ) {
    const first = (data as { exports: unknown[] }).exports[0];
    if (first !== undefined && first !== null && typeof first === 'object') {
      return first;
    }
  }
  return data;
}

export function getFiscalExportPreview(params: GetApiAdminFiscalExportParams) {
  return AXIOS_INSTANCE.get('/api/admin/fiscal-export', {
    params: { ...params, format: 'json' },
    headers: fiscalExportDisclaimerAckHeaders(),
  }).then((response) => unwrapFiscalExportEnvelope(response.data));
}

export async function downloadFiscalExportJson(params: GetApiAdminFiscalExportParams): Promise<Blob> {
  const response = await AXIOS_INSTANCE.get<Blob>('/api/admin/fiscal-export', {
    params: { ...params, format: 'jsonDownload' },
    responseType: 'blob',
    headers: fiscalExportDisclaimerAckHeaders(),
  });
  return response.data;
}

/**
 * WHY manual: Orval maps this endpoint to `Blob` because OpenAPI declares binary schema.
 * Removal: regenerate client after swagger exposes `RksvComplianceReportDto` as JSON 200.
 */
export function getRksvComplianceReportJson(
  params?: RksvComplianceReportQueryParams,
): Promise<RksvComplianceReport> {
  return AXIOS_INSTANCE.get<RksvComplianceReport>('/api/admin/rksv/compliance-report', {
    params: { ...params, format: 'json' },
  }).then((response) => response.data);
}

export async function downloadRksvComplianceReportPdf(
  params?: RksvComplianceReportQueryParams,
): Promise<Blob> {
  const response = await AXIOS_INSTANCE.get<Blob>('/api/admin/rksv/compliance-report', {
    params: { ...params, format: 'pdf' },
    responseType: 'blob',
  });
  return response.data;
}

export { extractApiErrorMessage } from '@/shared/errors/apiErrorMessageFallback';
