/**
 * Cross-app release-stage + environment badge helpers (FA + POS).
 * Prefer backend `releaseStage` from `/api/rksv/environment` when available;
 * build-time vars are a fallback for login / offline shells.
 *
 * Banner colors (operator UI):
 * - DEVELOPMENT → green
 * - STAGING → yellow
 * - CANARY → orange
 * - PRODUCTION → none (no banner)
 */

export type ReleaseStage = 'dev' | 'staging' | 'canary' | 'production';

export type EnvironmentBannerKind = 'development' | 'staging' | 'canary' | null;

export type EnvironmentBadgeColor = 'green' | 'gold' | 'orange' | 'blue';

export type EnvironmentBadgeSnapshot = {
  isDevelopment: boolean;
  isTest: boolean;
  isProduction: boolean;
  releaseStage: ReleaseStage;
};

export function normalizeReleaseStage(raw: string | null | undefined): ReleaseStage | null {
  if (raw == null) return null;
  const v = String(raw).trim().toLowerCase();
  if (v === 'dev' || v === 'development') return 'dev';
  if (v === 'staging' || v === 'stage') return 'staging';
  if (v === 'canary') return 'canary';
  if (v === 'production' || v === 'prod') return 'production';
  return null;
}

function readBuildTimeReleaseStage(): ReleaseStage | null {
  if (typeof process === 'undefined') return null;
  const fromPublic =
    process.env.NEXT_PUBLIC_RELEASE_STAGE ??
    process.env.EXPO_PUBLIC_RELEASE_STAGE ??
    process.env.RELEASE_STAGE;
  return normalizeReleaseStage(fromPublic);
}

export function readEnvironmentSnapshot(
  overrides?: Partial<EnvironmentBadgeSnapshot>,
): EnvironmentBadgeSnapshot {
  const nodeEnv = typeof process !== 'undefined' ? process.env.NODE_ENV : undefined;
  const rksvEnv =
    typeof process !== 'undefined'
      ? process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT?.trim().toUpperCase()
      : undefined;

  const isDevelopment = overrides?.isDevelopment ?? nodeEnv === 'development';
  const isProduction = overrides?.isProduction ?? nodeEnv === 'production';
  const isTest = overrides?.isTest ?? rksvEnv === 'TEST';

  let releaseStage: ReleaseStage =
    overrides?.releaseStage ??
    readBuildTimeReleaseStage() ??
    (isDevelopment ? 'dev' : isProduction ? 'production' : 'dev');

  return {
    isDevelopment,
    isTest,
    isProduction,
    releaseStage,
  };
}

/** Banner for host/release stage — production returns null. */
export function getReleaseStageBannerKind(
  releaseStage: ReleaseStage | string | null | undefined,
  options?: { isHostDevelopment?: boolean; isHostStaging?: boolean; isCanary?: boolean },
): EnvironmentBannerKind {
  if (options?.isCanary === true) return 'canary';
  const normalized = normalizeReleaseStage(
    typeof releaseStage === 'string' ? releaseStage : releaseStage ?? undefined,
  );
  if (normalized === 'canary') return 'canary';
  if (normalized === 'staging' || options?.isHostStaging === true) return 'staging';
  if (normalized === 'dev' || options?.isHostDevelopment === true) return 'development';
  return null;
}

export function getReleaseStageBannerLabel(kind: EnvironmentBannerKind): string {
  switch (kind) {
    case 'development':
      return 'DEVELOPMENT';
    case 'staging':
      return 'STAGING';
    case 'canary':
      return 'CANARY';
    default:
      return '';
  }
}

/** Ant Design Tag / Alert color tokens. */
export function getReleaseStageBannerColor(kind: EnvironmentBannerKind): EnvironmentBadgeColor {
  switch (kind) {
    case 'development':
      return 'green';
    case 'staging':
      return 'gold';
    case 'canary':
      return 'orange';
    default:
      return 'blue';
  }
}

export function getEnvironmentBadgeText(snapshot: EnvironmentBadgeSnapshot): string {
  const kind = getReleaseStageBannerKind(snapshot.releaseStage, {
    isHostDevelopment: snapshot.isDevelopment && snapshot.releaseStage === 'dev',
  });
  if (kind) return getReleaseStageBannerLabel(kind);
  if (snapshot.isTest) return 'TEST';
  return '';
}

export function getEnvironmentBadgeColor(
  snapshot: EnvironmentBadgeSnapshot,
): EnvironmentBadgeColor {
  const kind = getReleaseStageBannerKind(snapshot.releaseStage, {
    isHostDevelopment: snapshot.isDevelopment && snapshot.releaseStage === 'dev',
  });
  if (kind) return getReleaseStageBannerColor(kind);
  if (snapshot.isTest) return 'blue';
  return 'green';
}

export function getEnvironmentBadge(snapshot: EnvironmentBadgeSnapshot): {
  text: string;
  color: EnvironmentBadgeColor;
} | null {
  const text = getEnvironmentBadgeText(snapshot);
  if (!text) {
    return null;
  }
  return { text, color: getEnvironmentBadgeColor(snapshot) };
}

/** FA default snapshot (Next.js `NODE_ENV` + RELEASE_STAGE / RKSV public env). */
export const ENVIRONMENT_CONFIG = {
  get snapshot(): EnvironmentBadgeSnapshot {
    return readEnvironmentSnapshot();
  },
  get isDevelopment(): boolean {
    return this.snapshot.isDevelopment;
  },
  get isTest(): boolean {
    return this.snapshot.isTest;
  },
  get isProduction(): boolean {
    return this.snapshot.isProduction;
  },
  get releaseStage(): ReleaseStage {
    return this.snapshot.releaseStage;
  },
  getEnvironmentBadgeText(): string {
    return getEnvironmentBadgeText(this.snapshot);
  },
  getEnvironmentBadgeColor(): EnvironmentBadgeColor {
    return getEnvironmentBadgeColor(this.snapshot);
  },
  getEnvironmentBadge() {
    return getEnvironmentBadge(this.snapshot);
  },
};
