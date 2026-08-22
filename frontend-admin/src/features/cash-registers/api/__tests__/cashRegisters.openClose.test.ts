import { beforeEach, describe, expect, it, vi } from 'vitest';

import { closeCashRegister, openCashRegister } from '@/features/cash-registers/api/cashRegisters';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: { url: string; method: string; data?: unknown }) =>
    mockCustomInstance(config),
}));

describe('cashRegisters open/close API', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('opens via POST /api/admin/cash-registers/{id}/open', async () => {
    mockCustomInstance.mockResolvedValue({ message: 'ok' });
    await openCashRegister('reg-1', { openingBalance: 0 });
    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/cash-registers/reg-1/open',
        method: 'POST',
        data: { openingBalance: 0 },
      })
    );
  });

  it('closes via POST /api/admin/cash-registers/{id}/close', async () => {
    mockCustomInstance.mockResolvedValue({ message: 'ok' });
    await closeCashRegister('reg-1', { closingBalance: 12.5 });
    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/cash-registers/reg-1/close',
        method: 'POST',
        data: { closingBalance: 12.5 },
      })
    );
  });
});
