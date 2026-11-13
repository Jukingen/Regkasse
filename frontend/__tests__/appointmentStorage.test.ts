import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import {
  listHairSalonAppointments,
  saveHairSalonAppointment,
} from '../services/verticalProfiles/appointmentStorage';

const mockValues = new Map<string, string>();

jest.mock('../services/secureStorage', () => ({
  secureStorage: {
    getItem: (key: string) => Promise.resolve(mockValues.get(key) ?? null),
    setItem: (key: string, value: string) => {
      mockValues.set(key, value);
      return Promise.resolve();
    },
  },
}));

jest.mock('../services/tenant/tenantStorage', () => ({
  tenantStorage: {
    getTenantId: jest.fn<() => Promise<string | null>>().mockResolvedValue('tenant-a'),
  },
}));

describe('hair salon appointment storage', () => {
  beforeEach(() => {
    mockValues.clear();
  });

  it('saves and reloads a tenant-scoped booking', async () => {
    await saveHairSalonAppointment({
      customerName: 'Anna Beispiel',
      date: '2026-10-10',
      time: '10:30',
      serviceProductId: 'service-1',
      serviceName: 'Haarschnitt',
      durationMinutes: 45,
      staffId: 'staff-1',
    });

    const rows = await listHairSalonAppointments();
    expect(rows).toHaveLength(1);
    expect(rows[0]).toEqual(
      expect.objectContaining({
        customerName: 'Anna Beispiel',
        serviceName: 'Haarschnitt',
        staffId: 'staff-1',
      })
    );
  });
});
