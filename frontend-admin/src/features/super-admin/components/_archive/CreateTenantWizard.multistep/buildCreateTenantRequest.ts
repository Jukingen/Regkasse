import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';

import type { CreateAdminTenantRequest } from '@/features/super-admin/api/adminTenants';
import type { CreateTenantWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { normalizeTenantSlugInput } from '@/features/super-admin/lib/tenantSlug';

dayjs.extend(utc);

/** Maps wizard draft to POST /api/admin/tenants body. */
export function buildCreateTenantRequest(data: CreateTenantWizardData): CreateAdminTenantRequest {
  const slug = normalizeTenantSlugInput(data.slug);
  const licenseValidUntilUtc = dayjs
    .utc(data.licenseStartDate, 'YYYY-MM-DD')
    .add(data.licenseDays, 'day')
    .endOf('day')
    .toISOString();

  const password =
    data.passwordMode === 'manual' || data.adminPassword.trim()
      ? data.adminPassword.trim() || undefined
      : undefined;

  return {
    name: data.name.trim(),
    slug,
    email: data.email.trim(),
    phone: data.phone?.trim() || undefined,
    address: data.address?.trim() || undefined,
    adminEmail: data.adminEmail.trim(),
    adminPassword: password,
    grantTrialLicense: true,
    licenseValidUntilUtc,
    importDemoMenu: data.importDemoProducts,
    cashRegisterNumber: data.registerNumber.trim() || undefined,
    industryTemplateId:
      data.industryTemplateId && data.industryTemplateId !== 'none'
        ? data.industryTemplateId
        : null,
    seedIndustryStarterUsers: data.seedIndustryStarterUsers,
  };
}
