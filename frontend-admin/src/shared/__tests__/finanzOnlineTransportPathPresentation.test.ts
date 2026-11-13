import { describe, expect, it } from 'vitest';

import {
  FINANZ_ONLINE_TRANSPORT_PATH_KIND,
  finanzOnlineOutboxLifecyclePhaseTagColor,
  finanzOnlineOutboxStatusTagColor,
  finanzOnlineTransportPathTagColor,
  isSimulatedFinanzOnlineTransportPath,
  labelFinanzOnlineTransportPathKind,
} from '@/shared/finanzOnlineTransportPathPresentation';

describe('isSimulatedFinanzOnlineTransportPath', () => {
  it('is true only for the Simulated kind (operator simulated-transport badge)', () => {
    expect(isSimulatedFinanzOnlineTransportPath(FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated)).toBe(
      true
    );
    expect(isSimulatedFinanzOnlineTransportPath(FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest)).toBe(
      false
    );
    expect(
      isSimulatedFinanzOnlineTransportPath(FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction)
    ).toBe(false);
    expect(isSimulatedFinanzOnlineTransportPath(null)).toBe(false);
    expect(isSimulatedFinanzOnlineTransportPath('')).toBe(false);
  });
});

describe('finanzOnlineTransportPathPresentation', () => {
  it('colors simulated protocol success cautiously', () => {
    expect(
      finanzOnlineOutboxStatusTagColor(
        'ProtocolSuccess',
        FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated
      )
    ).toBe('warning');
    expect(
      finanzOnlineOutboxLifecyclePhaseTagColor(
        'ProtocolSuccess',
        FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated
      )
    ).toBe('warning');
  });

  it('colors real transport protocol success as strong success', () => {
    expect(
      finanzOnlineOutboxStatusTagColor(
        'ProtocolSuccess',
        FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest
      )
    ).toBe('success');
    expect(
      finanzOnlineOutboxStatusTagColor(
        'ProtocolSuccess',
        FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction
      )
    ).toBe('success');
  });

  it('maps transport path tag colors', () => {
    expect(finanzOnlineTransportPathTagColor(FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated)).toBe(
      'warning'
    );
    expect(finanzOnlineTransportPathTagColor(FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest)).toBe(
      'blue'
    );
    expect(
      finanzOnlineTransportPathTagColor(FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction)
    ).toBe('magenta');
  });

  it('falls back to raw kind when i18n returns the key unchanged', () => {
    const label = labelFinanzOnlineTransportPathKind((k) => k, 'UnknownKind', '—');
    expect(label).toBe('UnknownKind');
  });
});
