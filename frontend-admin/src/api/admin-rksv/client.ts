import { getApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type {
  GetApiAdminFiscalExportParams,
} from '@/api/generated/model';
import { AXIOS_INSTANCE } from '@/lib/axios';
import {
  normalizeCashRegisterListBody,
  type CashRegisterRow,
} from '@/features/tagesabschluss/normalizers';

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

export function getFiscalExportPreview(params: GetApiAdminFiscalExportParams) {
  return AXIOS_INSTANCE.get('/api/admin/fiscal-export', {
    params: { ...params, format: 'json' },
  }).then((response) => response.data);
}

export async function downloadFiscalExportJson(params: GetApiAdminFiscalExportParams): Promise<Blob> {
  const response = await AXIOS_INSTANCE.get<Blob>('/api/admin/fiscal-export', {
    params: { ...params, format: 'jsonDownload' },
    responseType: 'blob',
  });
  return response.data;
}

export { extractApiErrorMessage } from '@/shared/errors/apiErrorMessageFallback';
