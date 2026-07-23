import type { CompanySettings, UpdateCompanySettingsRequest } from '@/api/generated/model';
import {
  type SettingsFormValues,
  buildUpdateCompanySettingsRequest,
  mapSettingsToFormValues,
} from '@/features/settings/types/settingsForm';

/** RKSV company profile fields edited on `/settings/company`. */
export type CompanySettingsFormValues = Pick<
  SettingsFormValues,
  | 'companyName'
  | 'companyAddress'
  | 'companyTaxNumber'
  | 'companyPhone'
  | 'companyEmail'
  | 'companyWebsite'
  | 'companyDescription'
>;

export function mapCompanySettingsToFormValues(
  settings: CompanySettings | undefined | null
): Partial<CompanySettingsFormValues> {
  const mapped = mapSettingsToFormValues(settings ?? undefined);
  return {
    companyName: mapped.companyName,
    companyAddress: mapped.companyAddress,
    companyTaxNumber: mapped.companyTaxNumber,
    companyPhone: mapped.companyPhone,
    companyEmail: mapped.companyEmail,
    companyWebsite: mapped.companyWebsite,
    companyDescription: mapped.companyDescription,
  };
}

/** Merge company-only form values with existing tenant settings for a full PUT payload. */
export function mapCompanyFormToUpdateRequest(
  form: CompanySettingsFormValues,
  existing: CompanySettings | undefined | null
): UpdateCompanySettingsRequest {
  return buildUpdateCompanySettingsRequest(form, existing);
}
