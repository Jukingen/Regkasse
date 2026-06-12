import { describe, expect, it } from 'vitest';

import { buildPaymentMethodMatrix } from '@/features/payment-methods/utils/buildPaymentMethodMatrix';

describe('buildPaymentMethodMatrix', () => {
  it('builds rows and per-register summaries', () => {
    const registers = [
      { id: 'r1', registerNumber: 'K-01', location: 'Bar' },
      { id: 'r2', registerNumber: 'K-02', location: 'Terrasse' },
    ] as const;

    const { rows, summaries } = buildPaymentMethodMatrix(
      [...registers],
      {
        r1: [
          {
            id: '1',
            cashRegisterId: 'r1',
            code: 'cash',
            name: 'Bar',
            isActive: true,
            isDefault: true,
            displayOrder: 10,
            legacyPaymentMethodValue: 0,
            requiresTerminal: false,
            allowRefund: true,
            createdAtUtc: '',
          },
          {
            id: '2',
            cashRegisterId: 'r1',
            code: 'card',
            name: 'Karte',
            isActive: false,
            isDefault: false,
            displayOrder: 20,
            legacyPaymentMethodValue: 1,
            requiresTerminal: true,
            allowRefund: true,
            createdAtUtc: '',
          },
        ],
        r2: [
          {
            id: '3',
            cashRegisterId: 'r2',
            code: 'cash',
            name: 'Bar',
            isActive: true,
            isDefault: true,
            displayOrder: 10,
            legacyPaymentMethodValue: 0,
            requiresTerminal: false,
            allowRefund: true,
            createdAtUtc: '',
          },
        ],
      },
    );

    expect(rows).toHaveLength(2);
    expect(rows[0]?.code).toBe('cash');
    expect(rows[0]?.byRegister.r1?.isActive).toBe(true);
    expect(rows[0]?.byRegister.r2?.isActive).toBe(true);
    expect(rows[1]?.byRegister.r2).toBeNull();

    expect(summaries[0]?.activeCodes).toEqual(['cash']);
    expect(summaries[0]?.inactiveCodes).toEqual(['card']);
    expect(summaries[1]?.defaultCode).toBe('cash');
  });
});
