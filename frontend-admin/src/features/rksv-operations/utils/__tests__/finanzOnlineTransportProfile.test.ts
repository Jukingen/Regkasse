import { describe, expect, it } from 'vitest';

import {
  finanzOnlineTransportProfileTagColor,
  resolveFinanzOnlineTransportProfile,
  runtimeRequestForTransportProfile,
} from '@/features/rksv-operations/utils/finanzOnlineTransportProfile';

describe('finanzOnlineTransportProfile', () => {
  it('maps production host to locked production even if simulation flags are set', () => {
    expect(
      resolveFinanzOnlineTransportProfile({
        isProduction: true,
        useSimulation: true,
        enableRealTestSubmission: true,
        enableRealTestQuery: true,
      })
    ).toBe('production');
  });

  it('maps simulation to demo', () => {
    expect(
      resolveFinanzOnlineTransportProfile({
        isProduction: false,
        useSimulation: true,
        enableRealTestSubmission: false,
        enableRealTestQuery: false,
      })
    ).toBe('demo');
  });

  it('maps real TEST SOAP flags to bmfTest', () => {
    expect(
      resolveFinanzOnlineTransportProfile({
        isProduction: false,
        useSimulation: false,
        enableRealTestSubmission: true,
        enableRealTestQuery: true,
      })
    ).toBe('bmfTest');
  });

  it('maps real transport without TEST SOAP as incomplete', () => {
    expect(
      resolveFinanzOnlineTransportProfile({
        isProduction: false,
        useSimulation: false,
        enableRealTestSubmission: false,
        enableRealTestQuery: true,
      })
    ).toBe('incomplete');
  });

  it('builds overlay payloads without new backend fields', () => {
    expect(runtimeRequestForTransportProfile('demo')).toEqual({
      useSimulation: true,
      enableRealTestSubmission: false,
      enableRealTestQuery: false,
    });
    expect(runtimeRequestForTransportProfile('bmfTest')).toEqual({
      useSimulation: false,
      enableRealTestSubmission: true,
      enableRealTestQuery: true,
    });
  });

  it('uses distinct tag colors', () => {
    expect(finanzOnlineTransportProfileTagColor('demo')).toBe('gold');
    expect(finanzOnlineTransportProfileTagColor('bmfTest')).toBe('orange');
    expect(finanzOnlineTransportProfileTagColor('production')).toBe('red');
    expect(finanzOnlineTransportProfileTagColor('incomplete')).toBe('default');
  });
});
