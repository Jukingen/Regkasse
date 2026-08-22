import { describe, expect, it, jest, beforeEach } from '@jest/globals';

import {
  POS_SELECTABLE_REGISTERS_PATH,
  fetchPosSelectableRegisters,
} from '../services/api/cashRegisterService';
import { apiClient } from '../services/api/config';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
  },
}));

describe('fetchPosSelectableRegisters (POS selectable abstraction)', () => {
  beforeEach(() => {
    jest.mocked(apiClient.get).mockReset();
  });

  it('calls POS_SELECTABLE_REGISTERS_PATH (not GET /api/CashRegister inventory)', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({ registers: [] });
    await fetchPosSelectableRegisters();
    expect(apiClient.get).toHaveBeenCalledWith(POS_SELECTABLE_REGISTERS_PATH);
  });

  it('maps registers array from response', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', location: 'A' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toHaveLength(1);
    expect(emptyReason).toBeNull();
    expect(registers[0]).toMatchObject({
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      registerNumber: 'K1',
      location: 'A',
    });
  });

  it('maps assignedUserId from the selectable row', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        {
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          registerNumber: 'K1',
          assignedUserId: 'user-1',
        },
      ],
    });
    const { registers } = await fetchPosSelectableRegisters();
    expect(registers[0].assignedUserId).toBe('user-1');
  });

  it('empty selectable list yields empty registers and optional emptyReason', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({ registers: [], emptyReason: 'none_open' });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toEqual([]);
    expect(emptyReason).toBe('none_open');
    expect(apiClient.get).toHaveBeenCalledWith(POS_SELECTABLE_REGISTERS_PATH);
  });

  it('does not call legacy GET /CashRegister', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({ registers: [] });
    await fetchPosSelectableRegisters();
    expect(apiClient.get).not.toHaveBeenCalledWith('/CashRegister');
  });

  it('keeps Closed rows — they are opened by shift auto-open once picked', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', status: 'Closed' },
        { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', registerNumber: 'K2', status: 'Closed' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toHaveLength(2);
    expect(registers[0].status).toBe('Closed');
    expect(emptyReason).toBeNull();
  });

  it('surfaces none_assigned when every register belongs to another cashier', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({ registers: [], emptyReason: 'none_assigned' });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toEqual([]);
    expect(emptyReason).toBe('none_assigned');
  });

  it('mixed rows keep Open and Closed but drop states no shift can use', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', status: 'Closed' },
        { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', registerNumber: 'K2', status: 'Open' },
        { id: 'cccccccc-cccc-cccc-cccc-cccccccccccc', registerNumber: 'K3', status: 'Maintenance' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers.map((r) => r.id)).toEqual([
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    ]);
    expect(emptyReason).toBeNull();
  });

  it('reports none_selectable_for_user when the server only offered unusable rows', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', status: 'Maintenance' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toEqual([]);
    expect(emptyReason).toBe('none_selectable_for_user');
  });

  it('rows without status are unchanged (canonical selectable endpoint)', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1' },
        { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', registerNumber: 'K2' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toHaveLength(2);
    expect(emptyReason).toBeNull();
  });
});
