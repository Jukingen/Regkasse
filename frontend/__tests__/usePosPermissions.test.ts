import { describe, expect, it, jest } from '@jest/globals';
import { renderHook } from '@testing-library/react-native';

import { useAuth } from '../contexts/AuthContext';
import { usePosPermissions } from '../hooks/usePosPermissions';

jest.mock('../contexts/AuthContext', () => ({
  useAuth: jest.fn(),
}));

const mockUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;

describe('usePosPermissions', () => {
  it('reads permission flags from the signed-in user', async () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Cashier', permissions: ['payment.take', 'order.view'] },
    } as ReturnType<typeof useAuth>);

    const { result } = await renderHook(() => usePosPermissions());

    expect(result.current.canMakePayment).toBe(true);
    expect(result.current.canViewOrders).toBe(true);
    expect(result.current.isCashier).toBe(true);
    expect(result.current.canOpenShift).toBe(false);
    expect(result.current.canCreateSonderbeleg).toBe(false);
  });

  it('denies every flag when the session has no user', async () => {
    mockUseAuth.mockReturnValue({ user: null } as ReturnType<typeof useAuth>);

    const { result } = await renderHook(() => usePosPermissions());

    expect(result.current.canMakePayment).toBe(false);
    expect(result.current.canTakeOrders).toBe(false);
  });
});
