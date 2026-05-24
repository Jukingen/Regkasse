/**
 * Cross-app environment badge helpers (FA + POS).
 * Keep POS-specific `__DEV__` overrides in `frontend/shared/config/environmentBadge.ts`.
 */

export type EnvironmentBadgeColor = 'orange' | 'blue' | 'green';

export type EnvironmentBadgeSnapshot = {
  isDevelopment: boolean;
  isTest: boolean;
  isProduction: boolean;
};

export function readEnvironmentSnapshot(
  overrides?: Partial<EnvironmentBadgeSnapshot>,
): EnvironmentBadgeSnapshot {
  const nodeEnv = typeof process !== 'undefined' ? process.env.NODE_ENV : undefined;
  const rksvEnv =
    typeof process !== 'undefined'
      ? process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT?.trim().toUpperCase()
      : undefined;

  return {
    isDevelopment: overrides?.isDevelopment ?? nodeEnv === 'development',
    isTest: overrides?.isTest ?? rksvEnv === 'TEST',
    isProduction: overrides?.isProduction ?? nodeEnv === 'production',
  };
}

export function getEnvironmentBadgeText(snapshot: EnvironmentBadgeSnapshot): string {
  if (snapshot.isDevelopment) {
    return '🧪 Entwicklung';
  }
  if (snapshot.isTest) {
    return '🧪 TEST';
  }
  return '';
}

export function getEnvironmentBadgeColor(
  snapshot: EnvironmentBadgeSnapshot,
): EnvironmentBadgeColor {
  if (snapshot.isDevelopment) {
    return 'orange';
  }
  if (snapshot.isTest) {
    return 'blue';
  }
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

/** FA default snapshot (Next.js `NODE_ENV` + RKSV public env). */
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
