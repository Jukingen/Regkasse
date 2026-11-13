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
> & { thankYouMessage?: string };

export function mapCompanySettingsToFormValues(
  settings: CompanySettings | undefined | null
): Partial<CompanySettingsFormValues> {
  const mapped = mapSettingsToFormValues(settings ?? undefined);
  const extra = settings as (CompanySettings & { thankYouMessage?: string | null }) | undefined | null;
  return {
    companyName: mapped.companyName,
    companyAddress: mapped.companyAddress,
    companyTaxNumber: mapped.companyTaxNumber,
    companyPhone: mapped.companyPhone,
    companyEmail: mapped.companyEmail,
    companyWebsite: mapped.companyWebsite,
    companyDescription: mapped.companyDescription,
    thankYouMessage: extra?.thankYouMessage ?? '',
  };
}

/** Merge company-only form values with existing tenant settings for a full PUT payload. */
export function mapCompanyFormToUpdateRequest(
  form: CompanySettingsFormValues,
  existing: CompanySettings | undefined | null
): UpdateCompanySettingsRequest {
  const payload = buildUpdateCompanySettingsRequest(form, existing);
  return {
    ...payload,
    thankYouMessage: form.thankYouMessage,
  } as UpdateCompanySettingsRequest & { thankYouMessage?: string };
}
