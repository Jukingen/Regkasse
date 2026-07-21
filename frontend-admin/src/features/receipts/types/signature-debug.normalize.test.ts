import { describe, expect, it } from 'vitest';

import { normalizePaymentSignatureDebugPayload } from './signature-debug';

describe('normalizePaymentSignatureDebugPayload', () => {
  it('maps object with steps and compactJws', () => {
    const step = { stepId: 1, name: 'CMC match', status: 'PASS' as const, evidence: 'ok' };
    expect(
      normalizePaymentSignatureDebugPayload({
        steps: [step],
        compactJws: 'a.b.c',
      })
    ).toEqual({ steps: [step], compactJws: 'a.b.c' });
  });

  it('supports legacy array-only data', () => {
    const step = { stepId: 2, name: 'JWS format', status: 'FAIL' as const, evidence: 'x' };
    expect(normalizePaymentSignatureDebugPayload([step])).toEqual({
      steps: [step],
      compactJws: null,
    });
  });

  it('handles null and unknown shapes', () => {
    expect(normalizePaymentSignatureDebugPayload(null)).toEqual({ steps: [], compactJws: null });
    expect(normalizePaymentSignatureDebugPayload({})).toEqual({ steps: [], compactJws: null });
  });
});
