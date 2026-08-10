import type { BulkEmailRequest } from '@/api/generated/model';
import { LicenseType } from '@/api/generated/model/licenseType';
import { TenantStatus } from '@/api/generated/model/tenantStatus';

export type BulkEmailFormValues = {
  subject: string;
  body: string;
  filterByStatus?: TenantStatus;
  filterByLicenseType?: LicenseType;
  tenantIds?: string[];
};

export type BulkEmailValidationErrors = {
  subject?: 'required';
  body?: 'required';
};

/** Strip HTML tags / whitespace to detect empty rich-text bodies. */
export function stripHtmlToText(html: string): string {
  return html
    .replace(/<[^>]*>/g, ' ')
    .replace(/&nbsp;/gi, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function validateBulkEmailForm(
  values: Partial<BulkEmailFormValues>
): BulkEmailValidationErrors {
  const errors: BulkEmailValidationErrors = {};
  if (!values.subject?.trim()) {
    errors.subject = 'required';
  }
  if (!values.body || !stripHtmlToText(values.body)) {
    errors.body = 'required';
  }
  return errors;
}

export function isBulkEmailFormValid(values: Partial<BulkEmailFormValues>): boolean {
  return Object.keys(validateBulkEmailForm(values)).length === 0;
}

export function toBulkEmailRequest(values: BulkEmailFormValues): BulkEmailRequest {
  return {
    subject: values.subject.trim(),
    body: values.body,
    filterByStatus: values.filterByStatus,
    filterByLicenseType: values.filterByLicenseType,
    tenantIds: values.tenantIds?.length ? values.tenantIds : null,
  };
}

export const BULK_EMAIL_STATUS_OPTIONS: TenantStatus[] = [
  TenantStatus.Lead,
  TenantStatus.InOnboarding,
  TenantStatus.Active,
  TenantStatus.Suspended,
  TenantStatus.Cancelled,
  TenantStatus.Archived,
];

export const BULK_EMAIL_LICENSE_OPTIONS: LicenseType[] = [
  LicenseType.Trial,
  LicenseType.Starter,
  LicenseType.Business,
  LicenseType.Plus,
];

export const BULK_EMAIL_STATUS_LABEL_KEYS: Record<TenantStatus, string> = {
  [TenantStatus.Lead]: 'communication.bulkEmail.statusLead',
  [TenantStatus.InOnboarding]: 'communication.bulkEmail.statusInOnboarding',
  [TenantStatus.Active]: 'communication.bulkEmail.statusActive',
  [TenantStatus.Suspended]: 'communication.bulkEmail.statusSuspended',
  [TenantStatus.Cancelled]: 'communication.bulkEmail.statusCancelled',
  [TenantStatus.Archived]: 'communication.bulkEmail.statusArchived',
};
