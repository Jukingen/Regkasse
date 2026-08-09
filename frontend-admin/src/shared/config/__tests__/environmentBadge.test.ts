import { describe, expect, it } from 'vitest';

import {
  getEnvironmentBadge,
  getReleaseStageFromConfig,
  getReleaseStageTagColor,
  getReleaseStageTagLabel,
  readEnvironmentSnapshot,
} from '../../../../../shared/constants/environment';

describe('environment badge config', () => {
  it('returns DEVELOPMENT badge for development snapshot', () => {
    const badge = getEnvironmentBadge({
      isDevelopment: true,
      isTest: false,
      isProduction: false,
      releaseStage: 'dev',
    });
    expect(badge).toEqual({ text: 'DEVELOPMENT', color: 'green' });
  });

  it('returns STAGING badge when release stage is staging', () => {
    const badge = getEnvironmentBadge({
      isDevelopment: false,
      isTest: true,
      isProduction: false,
      releaseStage: 'staging',
    });
    expect(badge).toEqual({ text: 'STAGING', color: 'gold' });
  });

  it('returns null for production snapshot without non-prod stage', () => {
    expect(
      getEnvironmentBadge({
        isDevelopment: false,
        isTest: false,
        isProduction: true,
        releaseStage: 'production',
      })
    ).toBeNull();
  });

  it('readEnvironmentSnapshot respects overrides', () => {
    expect(
      readEnvironmentSnapshot({
        isDevelopment: true,
        isTest: true,
        isProduction: false,
        releaseStage: 'dev',
      })
    ).toEqual({
      isDevelopment: true,
      isTest: true,
      isProduction: false,
      releaseStage: 'dev',
    });
  });
});

describe('getReleaseStageFromConfig', () => {
  it('falls back to dev when unset or empty', () => {
    expect(getReleaseStageFromConfig(undefined)).toBe('dev');
    expect(getReleaseStageFromConfig(null)).toBe('dev');
    expect(getReleaseStageFromConfig('')).toBe('dev');
    expect(getReleaseStageFromConfig('   ')).toBe('dev');
  });

  it('normalizes known aliases', () => {
    expect(getReleaseStageFromConfig('staging')).toBe('staging');
    expect(getReleaseStageFromConfig('STAGE')).toBe('staging');
    expect(getReleaseStageFromConfig('development')).toBe('dev');
    expect(getReleaseStageFromConfig('canary')).toBe('canary');
    expect(getReleaseStageFromConfig('PROD')).toBe('production');
  });

  it('falls back to dev for unknown values', () => {
    expect(getReleaseStageFromConfig('not-a-stage')).toBe('dev');
  });

  it('exposes tag label and color for badges', () => {
    expect(getReleaseStageTagLabel('staging')).toBe('STAGING');
    expect(getReleaseStageTagColor('staging')).toBe('gold');
    expect(getReleaseStageTagLabel('production')).toBe('PRODUCTION');
    expect(getReleaseStageTagColor('production')).toBe('blue');
  });
});
