import { customInstance } from '@/lib/axios';

export type FiskalySignTestVatRow = {
  vatRate: string;
  amount: string;
};

export type FiskalySignTestScenario = {
  id: string;
  receiptType: string;
  canSign: boolean;
  description: string;
  amounts: FiskalySignTestVatRow[];
};

export type FiskalyQrValidation = {
  isValid: boolean;
  errors: string[];
  prefix?: string | null;
  cashRegisterSerial?: string | null;
  receiptNumber?: string | null;
  timestamp?: string | null;
};

export type FiskalyReceiptChecks = {
  qrFormatValid: boolean;
  hasReceiptNumber: boolean;
  receiptNumberLooksSequential: boolean;
  hasTimeSignature: boolean;
  hasCashRegisterSerial: boolean;
  signed: boolean;
};

export type FiskalySignTestResult = {
  success: boolean;
  scenario: string;
  receiptId: string;
  receiptNumber?: string | null;
  qrCodeData?: string | null;
  timeSignature?: number | null;
  signed: boolean;
  hints?: string[] | null;
  cashRegisterSerial?: string | null;
  receiptType?: string | null;
  environment?: string | null;
  fonValidationsJson?: string | null;
  qrValidation: FiskalyQrValidation;
  checks: FiskalyReceiptChecks;
};

export type FiskalyVerifyTestResult = Omit<FiskalySignTestResult, 'success' | 'scenario'>;

const BASE = '/api/admin/fiskaly-dev-test';

export async function getFiskalySignScenarios(
  signal?: AbortSignal
): Promise<FiskalySignTestScenario[]> {
  return customInstance<FiskalySignTestScenario[]>({
    url: `${BASE}/sign-scenarios`,
    method: 'GET',
    signal,
  });
}

export async function signFiskalyTestReceipt(
  cashRegisterId: string,
  scenario: string,
  signal?: AbortSignal
): Promise<FiskalySignTestResult> {
  return customInstance<FiskalySignTestResult>({
    url: `${BASE}/sign-test`,
    method: 'POST',
    data: { cashRegisterId, scenario },
    signal,
  });
}

export async function verifyFiskalyTestReceipt(
  cashRegisterId: string,
  receiptId: string,
  signal?: AbortSignal
): Promise<FiskalyVerifyTestResult> {
  return customInstance<FiskalyVerifyTestResult>({
    url: `${BASE}/verify-test`,
    method: 'POST',
    data: { cashRegisterId, receiptId },
    signal,
  });
}
