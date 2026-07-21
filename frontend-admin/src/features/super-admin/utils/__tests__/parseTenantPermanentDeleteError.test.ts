import axios from 'axios';
import { describe, expect, it } from 'vitest';

import {
  TenantPermanentDeleteBlockedError,
  parseTenantPermanentDeleteError,
} from '../parseTenantPermanentDeleteError';

describe('parseTenantPermanentDeleteError', () => {
  it('returns structured body from axios 400 responses', () => {
    const error = new axios.AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: {} as never,
      data: {
        code: 'cash_registers_present',
        message: 'Cannot permanently delete tenant with cash registers.',
        dependencies: {
          tenantId: '00000000-0000-0000-0000-000000000001',
          canHardDelete: false,
        },
      },
    });

    const parsed = parseTenantPermanentDeleteError(error);
    expect(parsed?.code).toBe('cash_registers_present');
    expect(parsed?.dependencies?.canHardDelete).toBe(false);
  });

  it('wraps structured failures in TenantPermanentDeleteBlockedError', () => {
    const blocked = new TenantPermanentDeleteBlockedError({
      code: 'production_policy',
      message: 'Disabled in production',
    });
    expect(blocked.response.code).toBe('production_policy');
    expect(blocked.message).toBe('Disabled in production');
  });
});
