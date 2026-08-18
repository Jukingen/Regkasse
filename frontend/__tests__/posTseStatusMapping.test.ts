import { describe, expect, it, afterEach } from '@jest/globals';

import { shouldShowPosTseTestBadge, toOperationalHealthFromPosTse } from '../utils/posTseStatus';

describe('toOperationalHealthFromPosTse', () => {
  it('prefers explicit operationalHealth', () => {
    expect(toOperationalHealthFromPosTse('Active', 'Degraded')).toBe('Degraded');
  });

  it('maps cashier indicator when operationalHealth is missing', () => {
    expect(toOperationalHealthFromPosTse('Active')).toBe('Online');
    expect(toOperationalHealthFromPosTse('Degraded')).toBe('Degraded');
    expect(toOperationalHealthFromPosTse('Inactive')).toBe('Offline');
  });
});

describe('shouldShowPosTseTestBadge', () => {
  const prevEnv = process.env.EXPO_PUBLIC_ENVIRONMENT;
  const prevStage = process.env.EXPO_PUBLIC_RELEASE_STAGE;

  afterEach(() => {
    if (prevEnv === undefined) delete process.env.EXPO_PUBLIC_ENVIRONMENT;
    else process.env.EXPO_PUBLIC_ENVIRONMENT = prevEnv;
    if (prevStage === undefined) delete process.env.EXPO_PUBLIC_RELEASE_STAGE;
    else process.env.EXPO_PUBLIC_RELEASE_STAGE = prevStage;
  });

  it('hides the badge when Fiskaly environment is LIVE', () => {
    process.env.EXPO_PUBLIC_ENVIRONMENT = 'TEST';
    expect(shouldShowPosTseTestBadge('LIVE')).toBe(false);
    expect(shouldShowPosTseTestBadge('PRODUCTION')).toBe(false);
  });

  it('shows the badge when Fiskaly environment is TEST', () => {
    process.env.EXPO_PUBLIC_ENVIRONMENT = 'LIVE';
    expect(shouldShowPosTseTestBadge('TEST')).toBe(true);
  });

  it('uses EXPO_PUBLIC_ENVIRONMENT when the API omits environment', () => {
    process.env.EXPO_PUBLIC_ENVIRONMENT = 'LIVE';
    process.env.EXPO_PUBLIC_RELEASE_STAGE = 'dev';
    expect(shouldShowPosTseTestBadge(null)).toBe(false);

    process.env.EXPO_PUBLIC_ENVIRONMENT = 'TEST';
    expect(shouldShowPosTseTestBadge(undefined)).toBe(true);
  });
});
