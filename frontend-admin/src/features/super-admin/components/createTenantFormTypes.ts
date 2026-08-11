/**
 * Form values for the single-step tenant creation wizard.
 */
export type CreateTenantFormValues = {
  name: string;
  slug: string;
  email: string;
  phone?: string;
  address?: string;
  grantTrialLicense?: boolean;
  importDemoProducts?: boolean;
  formError?: string;
};
