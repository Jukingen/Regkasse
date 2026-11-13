import { normalizeRksvBackendEnvironment } from '@/features/rksv/normalizeRksvBackendEnvironment';
import {
  isRksvBackendDemo,
  isTseFiscalConfigLockUnsafe,
} from '@/features/rksv/types/rksvBackendEnvironment';

describe('normalizeRksvBackendEnvironment', () => {
  it('maps demo environment from API', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Demo',
      isSimulated: true,
      showDemoLabel: true,
      tseStatusDisplay: 'TSE: SIMULIERT (NUR TEST)',
      tseStatusBadge: 'TSE SIMULIERT',
      environmentDisplayName: '🧪 DEMO / TEST',
      fiscalConfigLockOk: true,
      fiscalConfigLockEscapeHatchActive: false,
      fiscalConfigLockReasons: [],
    });

    expect(isRksvBackendDemo(dto)).toBe(true);
    expect(dto?.fiscalConfigLockOk).toBe(true);
    expect(isTseFiscalConfigLockUnsafe(dto)).toBe(false);
  });

  it('maps production environment from API', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Production',
      isSimulated: false,
      showDemoLabel: false,
      tseStatusDisplay: 'TSE: AKTIV ✅',
      tseStatusBadge: 'TSE AKTIV',
      environmentDisplayName: '🚀 PRODUCTION',
      fiscalConfigLockOk: true,
      fiscalConfigLockEscapeHatchActive: false,
      fiscalConfigLockReasons: [],
    });

    expect(isRksvBackendDemo(dto)).toBe(false);
  });

  it('maps production lock violations', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Production',
      isSimulated: false,
      showDemoLabel: false,
      fiscalConfigLockOk: false,
      fiscalConfigLockEscapeHatchActive: false,
      fiscalConfigLockReasons: ['Tse:Mode=Fake is forbidden in Production.'],
    });

    expect(dto?.fiscalConfigLockOk).toBe(false);
    expect(dto?.fiscalConfigLockReasons).toEqual([
      'Tse:Mode=Fake is forbidden in Production.',
    ]);
    expect(isTseFiscalConfigLockUnsafe(dto)).toBe(true);
  });

  it('maps escape hatch as unsafe for FA banner', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Production',
      isSimulated: true,
      fiscalConfigLockOk: true,
      fiscalConfigLockEscapeHatchActive: true,
      fiscalConfigLockReasons: ['Tse:TseMode must be Device in Production (Off/Demo are forbidden).'],
    });

    expect(isTseFiscalConfigLockUnsafe(dto)).toBe(true);
  });

  it('defaults lock ok when fields missing (legacy API)', () => {
    const dto = normalizeRksvBackendEnvironment({
      environment: 'Demo',
      isSimulated: true,
    });
    expect(dto?.fiscalConfigLockOk).toBe(true);
    expect(dto?.fiscalConfigLockEscapeHatchActive).toBe(false);
    expect(dto?.fiscalConfigLockReasons).toEqual([]);
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
        hostEnvironment: '',
        isHostDevelopment: false,
        isHostStaging: false,
        releaseStage: 'production',
        isCanary: false,
        isFinanzOnlineSimulated: false,
        isSimulationMode: false,
        fiscalConfigLockOk: true,
        fiscalConfigLockEscapeHatchActive: false,
        fiscalConfigLockReasons: [],
      })
    ).toBe(false);
  });
});
