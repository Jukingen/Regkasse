import { isSuperAdmin } from '@/features/auth/constants/roles';
import { findVerticalProfileCatalogEntry } from '@/features/vertical-profiles/verticalProfileCatalog';

export type AdminVerticalProfileSnapshot = {
  profileId: string;
  posFeatures: Record<string, boolean>;
  posLayout: string;
  isLoading: boolean;
  viewAllProfiles: boolean;
};

export const SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY = 'regkasse.admin.profileSimulation';

export const SUPER_ADMIN_PROFILE_SIMULATION_ALL = 'all';
export const SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL = 'actual';

export type SuperAdminProfileSimulation =
  typeof SUPER_ADMIN_PROFILE_SIMULATION_ALL | typeof SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL | string;

const listeners = new Set<() => void>();

function isKnownSimulation(value: string): boolean {
  return (
    value === SUPER_ADMIN_PROFILE_SIMULATION_ALL ||
    value === SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL ||
    findVerticalProfileCatalogEntry(value) != null
  );
}

export function readSuperAdminProfileSimulation(): SuperAdminProfileSimulation {
  if (typeof window === 'undefined') {
    return SUPER_ADMIN_PROFILE_SIMULATION_ALL;
  }
  const raw = window.localStorage.getItem(SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY)?.trim();
  if (!raw || !isKnownSimulation(raw)) {
    return SUPER_ADMIN_PROFILE_SIMULATION_ALL;
  }
  return raw;
}

export function writeSuperAdminProfileSimulation(value: SuperAdminProfileSimulation): void {
  if (typeof window === 'undefined') {
    return;
  }
  const next = isKnownSimulation(value) ? value : SUPER_ADMIN_PROFILE_SIMULATION_ALL;
  window.localStorage.setItem(SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY, next);
  listeners.forEach((listener) => listener());
}

export function subscribeSuperAdminProfileSimulation(listener: () => void): () => void {
  listeners.add(listener);
  const onStorage = (event: StorageEvent) => {
    if (event.key === SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY) {
      listener();
    }
  };
  if (typeof window !== 'undefined') {
    window.addEventListener('storage', onStorage);
  }
  return () => {
    listeners.delete(listener);
    if (typeof window !== 'undefined') {
      window.removeEventListener('storage', onStorage);
    }
  };
}

export function resolveEffectiveAdminVerticalProfile(input: {
  role: string | null | undefined;
  isImpersonating: boolean;
  simulation: SuperAdminProfileSimulation;
  actual: Omit<AdminVerticalProfileSnapshot, 'viewAllProfiles'>;
}): AdminVerticalProfileSnapshot {
  const actualView = { ...input.actual, viewAllProfiles: false };
  if (!isSuperAdmin(input.role) || input.isImpersonating) {
    return actualView;
  }
  if (input.simulation === SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL) {
    return actualView;
  }
  if (input.simulation !== SUPER_ADMIN_PROFILE_SIMULATION_ALL) {
    const simulated = findVerticalProfileCatalogEntry(input.simulation);
    if (simulated) {
      return {
        profileId: simulated.id,
        posFeatures: simulated.posFeatures,
        posLayout: simulated.posLayout,
        isLoading: false,
        viewAllProfiles: false,
      };
    }
  }
  return { ...input.actual, viewAllProfiles: true };
}
