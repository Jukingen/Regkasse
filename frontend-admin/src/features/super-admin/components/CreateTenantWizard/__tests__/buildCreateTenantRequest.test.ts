import { describe, expect, it } from 'vitest';

import { buildCreateTenantRequest } from '@/features/super-admin/components/CreateTenantWizard/buildCreateTenantRequest';
import { createEmptyWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { generateCompliantPassword } from '@/features/super-admin/lib/generateCompliantPassword';

describe('buildCreateTenantRequest', () => {
  it('maps wizard draft to API body with license end date and register number', () => {
    const data = {
      ...createEmptyWizardData(),
      name: 'Café Beispiel',
      slug: 'cafe-beispiel',
      email: 'kontakt@cafe.at',
      adminEmail: 'admin@cafe.at',
      passwordMode: 'auto' as const,
      adminPassword: '',
      registerNumber: 'KASSE-002',
      licenseDays: 90 as const,
      licenseStartDate: '2026-07-17',
      importDemoProducts: true,
    };

    const body = buildCreateTenantRequest(data);

    expect(body.name).toBe('Café Beispiel');
    expect(body.slug).toBe('cafe-beispiel');
    expect(body.adminEmail).toBe('admin@cafe.at');
    expect(body.adminPassword).toBeUndefined();
    expect(body.cashRegisterNumber).toBe('KASSE-002');
    expect(body.importDemoMenu).toBe(true);
    expect(body.grantTrialLicense).toBe(true);
    expect(body.licenseValidUntilUtc).toMatch(/^2026-10-15T/);
  });

  it('sends manual password when provided', () => {
    const data = {
      ...createEmptyWizardData(),
      name: 'Test GmbH',
      slug: 'test-gmbh',
      email: 'a@b.at',
      adminEmail: 'a@b.at',
      passwordMode: 'manual' as const,
      adminPassword: 'SecurePass1!',
      licenseStartDate: '2026-01-01',
      licenseDays: 30 as const,
    };

    const body = buildCreateTenantRequest(data);
    expect(body.adminPassword).toBe('SecurePass1!');
  });
});

describe('generateCompliantPassword', () => {
  it('meets length and character-class rules', () => {
    const password = generateCompliantPassword();
    expect(password.length).toBe(16);
    expect(/[a-z]/.test(password)).toBe(true);
    expect(/[A-Z]/.test(password)).toBe(true);
    expect(/\d/.test(password)).toBe(true);
    expect(/[^a-zA-Z0-9]/.test(password)).toBe(true);
  });
});
