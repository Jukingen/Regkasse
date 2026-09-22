import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { Alert } from 'react-native';

import { postCreateMonatsbeleg } from '../services/api/rksvSpecialReceiptsService';
import { apiClient } from '../services/api/config';
import {
  alertPosMonatsbelegCreateError,
  createPosMonatsbelegAndPrint,
} from '../utils/createPosMonatsbeleg';

jest.mock('../services/api/config', () => ({
  apiClient: {
    post: jest.fn(),
  },
}));

jest.mock('../services/receiptPrinter', () => ({
  receiptPrinter: {
    print: jest.fn(async () => undefined),
  },
}));

jest.spyOn(Alert, 'alert').mockImplementation(() => {});

describe('postCreateMonatsbeleg force query', () => {
  beforeEach(() => {
    jest.mocked(apiClient.post).mockReset();
    jest.mocked(Alert.alert).mockClear();
  });

  it('appends ?force=true and strips force from the JSON body', async () => {
    jest.mocked(apiClient.post).mockResolvedValue({
      paymentId: 'p1',
      invoiceId: 'i1',
      receiptId: 'r1',
      receiptNumber: '1',
      qrData: '',
    });

    await postCreateMonatsbeleg({
      cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      year: 2026,
      month: 8,
      reason: 'POS Monatsbeleg',
      force: true,
    });

    expect(apiClient.post).toHaveBeenCalledWith('/rksv/special-receipts/monatsbeleg?force=true', {
      cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      year: 2026,
      month: 8,
      reason: 'POS Monatsbeleg',
    });
  });

  it('keeps the path without query when force is omitted', async () => {
    jest.mocked(apiClient.post).mockResolvedValue({
      paymentId: 'p1',
      invoiceId: 'i1',
      receiptId: 'r1',
      receiptNumber: '1',
      qrData: '',
    });

    await postCreateMonatsbeleg({
      cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      year: 2026,
      month: 8,
    });

    expect(apiClient.post).toHaveBeenCalledWith('/rksv/special-receipts/monatsbeleg', {
      cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      year: 2026,
      month: 8,
    });
  });

  it('createPosMonatsbelegAndPrint forwards force=true to the special-receipts POST', async () => {
    jest.mocked(apiClient.post).mockResolvedValue({
      paymentId: 'pay-1',
      invoiceId: 'i1',
      receiptId: 'r1',
      receiptNumber: '1',
      qrData: '',
    });

    await createPosMonatsbelegAndPrint({
      cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      year: 2026,
      month: 8,
      force: true,
    });

    expect(apiClient.post).toHaveBeenCalledWith(
      '/rksv/special-receipts/monatsbeleg?force=true',
      expect.objectContaining({
        cashRegisterId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        year: 2026,
        month: 8,
      })
    );
  });

  it('shows warningMessage when 400 RequiresForce DTO is returned', () => {
    alertPosMonatsbelegCreateError(
      {
        status: 400,
        data: {
          requiresForce: true,
          warningMessage: 'Monatsbeleg für den Vormonat wird erstellt. Dies ist zulässig.',
        },
      },
      false
    );

    expect(Alert.alert).toHaveBeenCalledWith(
      'Monatsbeleg',
      'Monatsbeleg für den Vormonat wird erstellt. Dies ist zulässig.'
    );
  });
});
