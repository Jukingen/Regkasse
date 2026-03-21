import { describe, expect, it, jest, beforeEach } from '@jest/globals';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
  },
}));

import { apiClient } from '../services/api/config';
import {
  POS_SELECTABLE_REGISTERS_PATH,
  fetchPosSelectableRegisters,
} from '../services/api/cashRegisterService';

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

  it('inventory-style payload with only Closed rows yields empty registers and none_open (no false picker)', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', status: 'Closed' },
        { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', registerNumber: 'K2', status: 'Closed' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toEqual([]);
    expect(emptyReason).toBe('none_open');
  });

  it('mixed Open/Closed rows surfaces only Open for assignment', async () => {
    jest.mocked(apiClient.get).mockResolvedValue({
      registers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', registerNumber: 'K1', status: 'Closed' },
        { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', registerNumber: 'K2', status: 'Open' },
        { id: 'cccccccc-cccc-cccc-cccc-cccccccccccc', registerNumber: 'K3', status: 'closed' },
      ],
    });
    const { registers, emptyReason } = await fetchPosSelectableRegisters();
    expect(registers).toHaveLength(1);
    expect(registers[0].id).toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
    expect(emptyReason).toBeNull();
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
