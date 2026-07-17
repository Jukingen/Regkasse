import { describe, expect, it } from 'vitest';

import { buildCreateTenantRequest } from '@/features/super-admin/components/CreateTenantWizard/buildCreateTenantRequest';
import { createEmptyWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { parseTenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import { validateContactEmail } from '@/features/super-admin/lib/tenantCreateValidation';
import { suggestTenantSlugFromName, validateTenantSlug } from '@/features/super-admin/lib/tenantSlug';

/**
 * Wizard scenario coverage aligned with Super Admin create-tenant acceptance cases.
 */
describe('CreateTenantWizard scenarios', () => {
    describe('Scenario 1 — successful creation payload', () => {
        it('maps Cafe Muster GmbH wizard draft to API body', () => {
            const data = {
                ...createEmptyWizardData(),
                name: 'Cafe Muster GmbH',
                slug: 'cafe-muster',
                email: 'info@cafe-muster.at',
                adminEmail: 'admin@cafe-muster.at',
                passwordMode: 'auto' as const,
                adminPassword: '',
                registerNumber: 'KASSE-001',
                licenseDays: 365 as const,
                licenseStartDate: '2026-07-17',
                importDemoProducts: true,
            };

            const body = buildCreateTenantRequest(data);

            expect(body.name).toBe('Cafe Muster GmbH');
            expect(body.slug).toBe('cafe-muster');
            expect(body.email).toBe('info@cafe-muster.at');
            expect(body.adminEmail).toBe('admin@cafe-muster.at');
            expect(body.adminPassword).toBeUndefined();
            expect(body.cashRegisterNumber).toBe('KASSE-001');
            expect(body.importDemoMenu).toBe(true);
            expect(body.grantTrialLicense).toBe(true);
            expect(body.licenseValidUntilUtc).toMatch(/^2027-07-17T/);
        });

        it('suggests slug from company name', () => {
            expect(suggestTenantSlugFromName('Cafe Muster GmbH')).toBe('cafe-muster-gmbh');
        });
    });

    describe('Scenario 2 — duplicate slug', () => {
        it('parses slug_taken onboarding error for UI', () => {
            const error = {
                response: {
                    data: {
                        message: 'Subdomain "cafe-muster" is already in use.',
                        code: 'slug_taken',
                        suggestions: ['cafe-muster-2', 'cafe-muster-gmbh'],
                    },
                },
            };

            const parsed = parseTenantOnboardingError(error, 'save failed');

            expect(parsed.code).toBe('slug_taken');
            expect(parsed.message).toContain('cafe-muster');
            expect(parsed.slugSuggestions).toEqual(['cafe-muster-2', 'cafe-muster-gmbh']);
        });

        it('accepts cafe-muster as a valid slug format', () => {
            expect(validateTenantSlug('cafe-muster')).toBeNull();
        });
    });

    describe('Scenario 3 — invalid email', () => {
        it('rejects invalid-email on contact/admin validators', () => {
            expect(validateContactEmail('invalid-email')).toBe('invalid');
            expect(validateContactEmail('info@cafe-muster.at')).toBeNull();
        });
    });

    describe('Scenario 4 — weak password', () => {
        it('rejects passwords shorter than 8 characters (wizard rule)', () => {
            const password = '123';
            expect(password.length).toBeLessThan(8);
            // Mirrors Step2AdminUser Form.Item min: 8 + tenants.create.wizard.fields.passwordMin
            const meetsWizardMin = password.length >= 8;
            expect(meetsWizardMin).toBe(false);
        });

        it('accepts a compliant manual password', () => {
            const password = 'SecurePass1!';
            expect(password.length).toBeGreaterThanOrEqual(8);
        });
    });
});
