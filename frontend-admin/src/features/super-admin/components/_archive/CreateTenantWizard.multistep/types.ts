import type {
  AdminTenantDetail,
  TenantProvisioning,
} from '@/features/super-admin/api/adminTenants';

export type LicenseDaysOption = 30 | 90 | 365;

export type AdminPasswordMode = 'auto' | 'manual';

/** Shared draft across wizard steps (before API submit). */
export type CreateTenantWizardData = {
  name: string;
  slug: string;
  email: string;
  phone?: string;
  address?: string;
  adminEmail: string;
  adminPassword: string;
  passwordMode: AdminPasswordMode;
  registerNumber: string;
  licenseDays: LicenseDaysOption;
  /** Local calendar date YYYY-MM-DD */
  licenseStartDate: string;
  importDemoProducts: boolean;
  /** restaurant | retail | hotel | none */
  industryTemplateId: string;
  seedIndustryStarterUsers: boolean;
};

export type CreateTenantFormValues = CreateTenantWizardData & {
  grantTrialLicense?: boolean;
  autoDemoSetup?: boolean;
  formError?: string;
};

export const DEFAULT_REGISTER_NUMBER = 'KASSE-001';

export function createEmptyWizardData(): CreateTenantWizardData {
  const today = new Date();
  const yyyy = today.getFullYear();
  const mm = String(today.getMonth() + 1).padStart(2, '0');
  const dd = String(today.getDate()).padStart(2, '0');

  return {
    name: '',
    slug: '',
    email: '',
    phone: undefined,
    address: undefined,
    adminEmail: '',
    adminPassword: '',
    passwordMode: 'auto',
    registerNumber: DEFAULT_REGISTER_NUMBER,
    licenseDays: 365,
    licenseStartDate: `${yyyy}-${mm}-${dd}`,
    importDemoProducts: true,
    industryTemplateId: 'none',
    seedIndustryStarterUsers: true,
  };
}

export type CreateTenantWizardProps = {
  open: boolean;
  onClose: () => void;
  onCreated?: (detail: AdminTenantDetail) => void;
  onCreateAnother?: () => void;
  onSwitchToTenant?: (tenantId: string) => void;
  switchToTenantLoading?: boolean;
};

export type WizardStepKey = 'tenantInfo' | 'adminUser' | 'registerLicense' | 'summary' | 'result';

export const WIZARD_STEP_KEYS: WizardStepKey[] = [
  'tenantInfo',
  'adminUser',
  'registerLicense',
  'summary',
  'result',
];

export type TenantOnboardingSuccessState = {
  tenantId: string;
  tenantName: string;
  slug: string;
  contactEmail: string;
  provisioning: TenantProvisioning | null;
};

export type StepCommonProps = {
  data: CreateTenantWizardData;
  onUpdate: (patch: Partial<CreateTenantWizardData>) => void;
};
