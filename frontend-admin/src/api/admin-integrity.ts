/**
 * Admin fiscal/data integrity report — GET /api/admin/integrity (read-only).
 * Requires backend permission audit.view.
 */
import { customInstance } from '@/lib/axios';

export interface SequenceIssuesDto {
  duplicateReceiptNumberCount: number;
  nonMonotonicSequenceCount: number;
  duplicateReceiptNumbers?: string[];
  nonMonotonicKeys?: string[];
}

export interface OrphanRefundsDto {
  orphanRefundCount: number;
  missingOriginalPaymentCount: number;
  refundWithoutInvoiceCount: number;
  orphanPaymentIds?: string[];
  refundReceiptNumbersMissingInvoice?: string[];
}

export interface PaymentWithoutInvoiceDto {
  count: number;
  paymentIds?: string[];
}

export interface IntegrityReportDto {
  sequenceIssues: SequenceIssuesDto;
  orphanRefunds: OrphanRefundsDto;
  paymentWithoutInvoice: PaymentWithoutInvoiceDto;
  generatedAtUtc: string;
}

export interface GetIntegrityReportParams {
  fromDate?: string;
  toDate?: string;
  includeDetails?: boolean;
}

export async function getIntegrityReport(params: GetIntegrityReportParams = {}): Promise<IntegrityReportDto> {
  const search = new URLSearchParams();
  if (params.fromDate) search.set('fromDate', params.fromDate);
  if (params.toDate) search.set('toDate', params.toDate);
  if (params.includeDetails) search.set('includeDetails', 'true');
  const qs = search.toString();
  return customInstance<IntegrityReportDto>({
    url: qs ? `/api/admin/integrity?${qs}` : '/api/admin/integrity',
    method: 'GET',
  });
}
