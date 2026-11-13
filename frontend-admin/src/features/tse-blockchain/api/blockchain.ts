import { customInstance } from '@/lib/axios';

import type {
  StoreTseBlockchainSignatureRequest,
  TseBlockchainRecord,
  TseBlockchainStatus,
  TseBlockchainTransaction,
} from '../types';

export async function getTseBlockchainStatus(
  signal?: AbortSignal
): Promise<TseBlockchainStatus> {
  return customInstance<TseBlockchainStatus>({
    url: '/api/admin/tse/blockchain/status',
    method: 'GET',
    signal,
  });
}

export async function syncTseBlockchain(): Promise<TseBlockchainStatus> {
  return customInstance<TseBlockchainStatus>({
    url: '/api/admin/tse/blockchain/sync',
    method: 'POST',
  });
}

export async function listTseBlockchainTransactions(
  tenantId: string,
  take = 50,
  signal?: AbortSignal
): Promise<TseBlockchainTransaction[]> {
  return customInstance<TseBlockchainTransaction[]>({
    url: '/api/admin/tse/blockchain/transactions',
    method: 'GET',
    params: { tenantId, take },
    signal,
  });
}

export async function storeTseBlockchainSignature(
  body: StoreTseBlockchainSignatureRequest
): Promise<TseBlockchainRecord> {
  return customInstance<TseBlockchainRecord>({
    url: '/api/admin/tse/blockchain/store',
    method: 'POST',
    data: body,
  });
}
