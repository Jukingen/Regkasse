import { normalizeRksvBackendEnvironment } from '@/features/rksv/normalizeRksvBackendEnvironment';
import { isRksvBackendDemo } from '@/features/rksv/types/rksvBackendEnvironment';

describe('normalizeRksvBackendEnvironment', () => {
  it('maps demo environment from API', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Demo',
      isSimulated: true,
      showDemoLabel: true,
      tseStatusDisplay: 'TSE: SIMULIERT (NUR TEST)',
      tseStatusBadge: 'TSE SIMULIERT',
      environmentDisplayName: '🧪 DEMO / TEST',
    });

    expect(isRksvBackendDemo(dto)).toBe(true);
  });

  it('maps production environment from API', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Production',
      isSimulated: false,
      showDemoLabel: false,
      tseStatusDisplay: 'TSE: AKTIV ✅',
      tseStatusBadge: 'TSE AKTIV',
      environmentDisplayName: '🚀 PRODUCTION',
    });

    expect(isRksvBackendDemo(dto)).toBe(false);
  });

  it('uses isSimulated only (not environment name alone)', () => {
    expect(
      isRksvBackendDemo({
        environment: 'Demo',
        isSimulated: false,
        showDemoLabel: true,
        tseStatusDisplay: '',
        tseStatusBadge: '',
        environmentDisplayName: '',
      })
    ).toBe(false);
  });
});
