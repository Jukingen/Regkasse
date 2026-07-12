import { describe, expect, it } from '@jest/globals';

import { normalizeRksvEnvironmentStatus } from '../utils/normalizeRksvEnvironment';
import { isRksvDemoEnvironment } from '../services/api/rksvEnvironmentTypes';

describe('normalizeRksvEnvironmentStatus', () => {
  it('maps camelCase backend payload', () => {
    const dto = normalizeRksvEnvironmentStatus({
      environment: 'Demo',
      isSimulated: true,
      showDemoLabel: true,
      tseStatusDisplay: 'TSE: SIMULIERT (NUR TEST)',
      tseStatusBadge: 'TSE SIMULIERT',
      environmentDisplayName: '🧪 DEMO / TEST',
    });

    expect(dto).toEqual({
      environment: 'Demo',
      isSimulated: true,
      showDemoLabel: true,
      tseStatusDisplay: 'TSE: SIMULIERT (NUR TEST)',
      tseStatusBadge: 'TSE SIMULIERT',
      environmentDisplayName: '🧪 DEMO / TEST',
    });
    expect(isRksvDemoEnvironment(dto)).toBe(true);
  });

  it('maps PascalCase backend payload', () => {
    const dto = normalizeRksvEnvironmentStatus({
      Environment: 'Production',
      IsSimulated: false,
      ShowDemoLabel: false,
      TseStatusDisplay: 'TSE: AKTIV ✅',
      TseStatusBadge: 'TSE AKTIV',
      EnvironmentDisplayName: '🚀 PRODUCTION',
    });

    expect(dto?.environment).toBe('Production');
    expect(isRksvDemoEnvironment(dto)).toBe(false);
  });
});
