import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

import { useAuth } from './AuthContext';
import { apiClient } from '../services/api/config';
import { secureStorage } from '../services/secureStorage';
import { tenantStorage } from '../services/tenant/tenantStorage';

export type PosLayout = 'standard' | 'tables' | 'appointment' | 'queue' | 'taxi' | 'ticket' | 'rooms';
export type PosFeatureMap = Record<string, boolean>;
export type VerticalFieldMap = Record<string, string[]>;

export interface EffectiveVerticalProfile {
  profileId: string;
  name?: string | null;
  posFeatures: PosFeatureMap;
  requiredFields: VerticalFieldMap;
  optionalFields: VerticalFieldMap;
  posLayout: PosLayout;
  taxiTariffPerKm: number | null;
}

type VerticalProfileSource = 'default' | 'cache' | 'network';

interface VerticalProfileContextValue extends EffectiveVerticalProfile {
  isLoading: boolean;
  error: string | null;
  source: VerticalProfileSource;
  refresh: () => Promise<void>;
}

const DEFAULT_PROFILE: EffectiveVerticalProfile = {
  profileId: 'default',
  posFeatures: {},
  requiredFields: { customer: [], product: [] },
  optionalFields: { customer: [], product: [] },
  posLayout: 'standard',
  taxiTariffPerKm: null,
};

const VerticalProfileContext = createContext<VerticalProfileContextValue | undefined>(undefined);

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function normalizeFeatures(value: unknown): PosFeatureMap {
  return Object.fromEntries(
    Object.entries(asRecord(value)).filter((entry): entry is [string, boolean] => {
      return typeof entry[1] === 'boolean';
    })
  );
}

function normalizeFields(value: unknown): VerticalFieldMap {
  return Object.fromEntries(
    Object.entries(asRecord(value))
      .filter((entry): entry is [string, unknown[]] => Array.isArray(entry[1]))
      .map(([entity, fields]) => [
        entity,
        fields.filter((field): field is string => typeof field === 'string'),
      ])
  );
}

function normalizeLayout(value: unknown): PosLayout {
  return value === 'tables' ||
    value === 'appointment' ||
    value === 'queue' ||
    value === 'taxi' ||
    value === 'ticket' ||
    value === 'rooms'
    ? value
    : 'standard';
}

function normalizeTariff(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value) && value >= 0) return value;
  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value.replace(',', '.'));
    if (Number.isFinite(parsed) && parsed >= 0) return parsed;
  }
  return null;
}

export function normalizeVerticalProfile(value: unknown): EffectiveVerticalProfile | null {
  const raw = asRecord(value);
  if (typeof raw.profileId !== 'string' || raw.profileId.trim().length === 0) {
    return null;
  }

  const posFeatures = normalizeFeatures(raw.posFeatures);
  const requiredFields = normalizeFields(raw.requiredFields);
  const optionalFields = normalizeFields(raw.optionalFields);

  // The current backend field schema starts with customer/product. Keep arbitrary
  // future entity groups, and expose the taxi route field until order fields become
  // a first-class server profile group.
  if (posFeatures.routeTracking && !optionalFields.order?.includes('route')) {
    optionalFields.order = [...(optionalFields.order ?? []), 'route'];
  }
  if (posFeatures.imeiTracking && !optionalFields.product?.includes('imei')) {
    optionalFields.product = [...(optionalFields.product ?? []), 'imei'];
  }

  return {
    profileId: raw.profileId.trim(),
    name: typeof raw.name === 'string' ? raw.name : null,
    posFeatures,
    requiredFields: {
      customer: [],
      product: [],
      ...requiredFields,
    },
    optionalFields: {
      customer: [],
      product: [],
      ...optionalFields,
    },
    posLayout: normalizeLayout(raw.posLayout),
    taxiTariffPerKm: normalizeTariff(raw.taxiTariffPerKm),
  };
}

function cacheKey(scope: string): string {
  return `vertical_profile_v1_${scope.replace(/[^a-zA-Z0-9_-]/g, '_')}`;
}

function firstNonBlank(...values: (string | null | undefined)[]): string | null {
  for (const value of values) {
    const normalized = value?.trim();
    if (normalized) return normalized;
  }
  return null;
}

export function VerticalProfileProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isAuthReady, user } = useAuth();
  const userId = user?.id;
  const userTenantId = user?.tenantId;
  const userTenantSlug = user?.tenantSlug;
  const [profile, setProfile] = useState<EffectiveVerticalProfile>(DEFAULT_PROFILE);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [source, setSource] = useState<VerticalProfileSource>('default');
  const loadGenerationRef = useRef(0);

  const load = useCallback(async () => {
    const generation = ++loadGenerationRef.current;
    if (!isAuthReady || !isAuthenticated || !userId) {
      setProfile(DEFAULT_PROFILE);
      setSource('default');
      setError(null);
      setIsLoading(false);
      return;
    }

    // Fail closed between tenant/session changes so one tenant's cached features are
    // never rendered while the next tenant scope is being resolved.
    setProfile(DEFAULT_PROFILE);
    setSource('default');
    setError(null);
    setIsLoading(true);

    const tenantScope = firstNonBlank(
      userTenantId,
      await tenantStorage.getTenantId(),
      userTenantSlug,
      await tenantStorage.getTenantSlug()
    );
    if (generation !== loadGenerationRef.current) return;
    if (!tenantScope) {
      setProfile(DEFAULT_PROFILE);
      setSource('default');
      setError('TENANT_SCOPE_MISSING');
      setIsLoading(false);
      return;
    }

    const key = cacheKey(tenantScope);

    try {
      const cachedRaw = await secureStorage.getItem(key);
      const cached = cachedRaw ? normalizeVerticalProfile(JSON.parse(cachedRaw)) : null;
      if (cached && generation === loadGenerationRef.current) {
        setProfile(cached);
        setSource('cache');
      }
    } catch {
      // Invalid/missing cache is ignored; the authenticated network read below is authoritative.
    }

    try {
      const fresh = normalizeVerticalProfile(
        await apiClient.get<unknown>('/pos/vertical-profile')
      );
      if (!fresh) throw new Error('INVALID_VERTICAL_PROFILE');
      if (generation !== loadGenerationRef.current) return;
      setProfile(fresh);
      setSource('network');
      await secureStorage.setItem(key, JSON.stringify(fresh));
    } catch {
      if (generation === loadGenerationRef.current) {
        setError('VERTICAL_PROFILE_LOAD_FAILED');
      }
    } finally {
      if (generation === loadGenerationRef.current) {
        setIsLoading(false);
      }
    }
  }, [isAuthReady, isAuthenticated, userId, userTenantId, userTenantSlug]);

  useEffect(() => {
    // Provider bootstrap intentionally synchronizes async tenant state into React.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load().catch(() => undefined);
  }, [load]);

  const value = useMemo<VerticalProfileContextValue>(
    () => ({
      ...profile,
      isLoading,
      error,
      source,
      refresh: load,
    }),
    [profile, isLoading, error, source, load]
  );

  return <VerticalProfileContext.Provider value={value}>{children}</VerticalProfileContext.Provider>;
}

export function useVerticalProfileContext(): VerticalProfileContextValue {
  const context = useContext(VerticalProfileContext);
  if (!context) {
    throw new Error('useVerticalProfileContext must be used within VerticalProfileProvider');
  }
  return context;
}

export function useVerticalFeatures() {
  const { profileId, posFeatures, requiredFields, optionalFields, posLayout, taxiTariffPerKm } =
    useVerticalProfileContext();
  return useMemo(
    () => ({ profileId, posFeatures, requiredFields, optionalFields, posLayout, taxiTariffPerKm }),
    [profileId, posFeatures, requiredFields, optionalFields, posLayout, taxiTariffPerKm]
  );
}
