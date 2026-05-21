/**
 * Onboarding progress steps shown while POST /api/admin/tenants runs (atomic server provisioning).
 */
export type TenantOnboardingStepId =
    | 'company'
    | 'subdomain'
    | 'admin'
    | 'license'
    | 'register'
    | 'products'
    | 'handoff';

export type TenantOnboardingStepStatus = 'pending' | 'active' | 'done' | 'error' | 'skipped';

export type TenantOnboardingStepDefinition = {
    id: TenantOnboardingStepId;
    /** i18n key under tenants.create.processing.steps */
    labelKey: string;
    /** Shown when step is done (success line). */
    doneKey: string;
};

export const TENANT_ONBOARDING_STEP_INTERVAL_MS = 550;

export function buildTenantOnboardingSteps(grantTrialLicense: boolean): TenantOnboardingStepDefinition[] {
    const steps: TenantOnboardingStepDefinition[] = [
        {
            id: 'company',
            labelKey: 'tenants.create.processing.steps.company',
            doneKey: 'tenants.create.processing.steps.companyDone',
        },
        {
            id: 'subdomain',
            labelKey: 'tenants.create.processing.steps.subdomain',
            doneKey: 'tenants.create.processing.steps.subdomainDone',
        },
        {
            id: 'admin',
            labelKey: 'tenants.create.processing.steps.admin',
            doneKey: 'tenants.create.processing.steps.adminDone',
        },
    ];

    if (grantTrialLicense) {
        steps.push({
            id: 'license',
            labelKey: 'tenants.create.processing.steps.license',
            doneKey: 'tenants.create.processing.steps.licenseDone',
        });
    }

    steps.push(
        {
            id: 'register',
            labelKey: 'tenants.create.processing.steps.register',
            doneKey: 'tenants.create.processing.steps.registerDone',
        },
        {
            id: 'products',
            labelKey: 'tenants.create.processing.steps.products',
            doneKey: 'tenants.create.processing.steps.productsDone',
        },
        {
            id: 'handoff',
            labelKey: 'tenants.create.processing.steps.handoff',
            doneKey: 'tenants.create.processing.steps.handoffDone',
        },
    );

    return steps;
}

export function resolveStepStatuses(
    definitions: TenantOnboardingStepDefinition[],
    activeIndex: number,
    phase: 'running' | 'success' | 'error',
    errorStepId?: TenantOnboardingStepId,
): Record<TenantOnboardingStepId, TenantOnboardingStepStatus> {
    const result = {} as Record<TenantOnboardingStepId, TenantOnboardingStepStatus>;

    definitions.forEach((def, index) => {
        if (phase === 'success') {
            result[def.id] = 'done';
            return;
        }

        if (phase === 'error') {
            if (def.id === errorStepId) {
                result[def.id] = 'error';
                return;
            }
            if (index < activeIndex) {
                result[def.id] = 'done';
                return;
            }
            result[def.id] = 'pending';
            return;
        }

        if (index < activeIndex) {
            result[def.id] = 'done';
        } else if (index === activeIndex) {
            result[def.id] = 'active';
        } else {
            result[def.id] = 'pending';
        }
    });

    return result;
}
