import type { UpdateFinanzOnlineRuntimeRequest } from '@/features/rksv-operations/api/finanzOnlineOutboxWorker';

export type FinanzOnlineTransportProfile = 'demo' | 'bmfTest' | 'production' | 'incomplete';

export type FinanzOnlineTransportProfileInput = {
  isProduction: boolean;
  useSimulation: boolean;
  enableRealTestSubmission: boolean;
  enableRealTestQuery: boolean;
};

/** Derive the operator-facing FON profile from existing overlay DTO fields (no extra API). */
export function resolveFinanzOnlineTransportProfile(
  input: FinanzOnlineTransportProfileInput
): FinanzOnlineTransportProfile {
  if (input.isProduction) return 'production';
  if (input.useSimulation) return 'demo';
  if (input.enableRealTestSubmission && input.enableRealTestQuery) return 'bmfTest';
  return 'incomplete';
}

export function runtimeRequestForTransportProfile(
  profile: Extract<FinanzOnlineTransportProfile, 'demo' | 'bmfTest'>
): UpdateFinanzOnlineRuntimeRequest {
  if (profile === 'demo') {
    return {
      useSimulation: true,
      enableRealTestSubmission: false,
      enableRealTestQuery: false,
    };
  }

  return {
    useSimulation: false,
    enableRealTestSubmission: true,
    enableRealTestQuery: true,
  };
}

export function finanzOnlineTransportProfileTagColor(
  profile: FinanzOnlineTransportProfile
): string {
  switch (profile) {
    case 'demo':
      return 'gold';
    case 'bmfTest':
      return 'orange';
    case 'production':
      return 'red';
    default:
      return 'default';
  }
}
