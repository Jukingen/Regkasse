/**
 * Form values for the two-step tenant creation wizard (country → tenant form).
 */
export type CreateTenantFormValues = {
  countryCode: string;
  vatRegime: string;
  name: string;
  slug: string;
  email: string;
  phone?: string;
  address?: string;
  grantTrialLicense?: boolean;
  trialDurationDays?: 14 | 30 | 60 | 90;
  importDemoProducts?: boolean;
  formError?: string;
};
