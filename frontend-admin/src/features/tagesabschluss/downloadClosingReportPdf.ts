import { reportPdfTypeFromClosingType } from '@/features/reports/api/reportPdfApi';
import { buildReportFileName } from '@/features/reports/utils/reportExportFileName';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { customInstance } from '@/lib/axios';

export async function downloadClosingReportPdf(
  closingId: string,
  options?: {
    language?: string;
    closingType?: string | null;
    fileName?: string;
    tenantSlug?: string | null;
    businessDate?: Date | string | null;
  }
): Promise<void> {
  const lang = encodeURIComponent((options?.language ?? 'de').split('-')[0] || 'de');
  const blob = await customInstance<Blob>({
    url: `/api/Tagesabschluss/closing/${encodeURIComponent(closingId)}/report.pdf?language=${lang}`,
    method: 'GET',
    responseType: 'blob',
  });
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download =
    options?.fileName ??
    buildReportFileName({
      reportType: reportPdfTypeFromClosingType(options?.closingType),
      tenantSlug: options?.tenantSlug ?? getEffectiveTenantSlug(),
      businessDate: options?.businessDate,
    });
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}
