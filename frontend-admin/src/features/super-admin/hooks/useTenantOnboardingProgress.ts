import { useEffect, useMemo, useState } from 'react';

import {
  TENANT_ONBOARDING_STEP_INTERVAL_MS,
  type TenantOnboardingStepDefinition,
  type TenantOnboardingStepId,
  type TenantOnboardingStepStatus,
  buildTenantOnboardingSteps,
  resolveStepStatuses,
} from '@/features/super-admin/lib/tenantOnboardingSteps';

export type TenantOnboardingProgressPhase = 'idle' | 'running' | 'success' | 'error';

export function useTenantOnboardingProgress(
  grantTrialLicense: boolean,
  phase: TenantOnboardingProgressPhase
) {
  const definitions = useMemo(
    () => buildTenantOnboardingSteps(grantTrialLicense),
    [grantTrialLicense]
  );

  const [activeIndex, setActiveIndex] = useState(0);

  useEffect(() => {
    if (phase === 'idle') {
      setActiveIndex(0);
      return;
    }

    if (phase === 'success') {
      setActiveIndex(definitions.length);
      return;
    }

    if (phase !== 'running') {
      return;
    }

    setActiveIndex(0);
    const timer = window.setInterval(() => {
      setActiveIndex((prev) => Math.min(prev + 1, definitions.length - 1));
    }, TENANT_ONBOARDING_STEP_INTERVAL_MS);

    return () => window.clearInterval(timer);
  }, [phase, definitions.length]);

  const errorStepId: TenantOnboardingStepId | undefined =
    phase === 'error'
      ? definitions[Math.min(activeIndex, Math.max(definitions.length - 1, 0))]?.id
      : undefined;

  const statuses: Record<TenantOnboardingStepId, TenantOnboardingStepStatus> = useMemo(() => {
    if (phase === 'idle') {
      return resolveStepStatuses(definitions, 0, 'running');
    }
    if (phase === 'success') {
      return resolveStepStatuses(definitions, definitions.length, 'success');
    }
    if (phase === 'error') {
      return resolveStepStatuses(definitions, activeIndex, 'error', errorStepId);
    }
    return resolveStepStatuses(definitions, activeIndex, 'running');
  }, [definitions, activeIndex, phase, errorStepId]);

  return { definitions, statuses, activeIndex };
}
