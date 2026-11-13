import { describe, expect, it, jest, beforeEach } from '@jest/globals';

import {
  POS_OPEN_REQUESTS_MINE_PATH,
  POS_OPEN_REQUESTS_PATH,
  fetchMyPosCashRegisterOpenRequests,
  requestPosCashRegisterOpen,
} from '../services/api/cashRegisterService';
import { apiClient } from '../services/api/config';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

describe('POS cash register open requests', () => {
  beforeEach(() => {
    jest.mocked(apiClient.get).mockReset();
    jest.mocked(apiClient.post).mockReset();
  });

  it('posts to the POS open-request path', async () => {
    jest.mocked(apiClient.post).mockResolvedValue({
      succeeded: true,
      request: {
        id: 'req-1',
        cashRegisterId: 'reg-1',
        status: 'Pending',
        requestedAt: '2026-09-14T12:00:00Z',
      },
    });
    const result = await requestPosCashRegisterOpen('reg-1');
    expect(apiClient.post).toHaveBeenCalledWith(POS_OPEN_REQUESTS_PATH, { registerId: 'reg-1' });
    expect(result.succeeded).toBe(true);
    expect(result.request?.id).toBe('req-1');
  });

  it('maps mine list rows', async () => {
    jest.mocked(apiClient.get).mockResolvedValue([
      {
        id: 'req-2',
        cashRegisterId: 'reg-2',
        status: 'Pending',
        requestedAt: '2026-09-14T12:00:00Z',
      },
    ]);
    const rows = await fetchMyPosCashRegisterOpenRequests();
    expect(apiClient.get).toHaveBeenCalledWith(POS_OPEN_REQUESTS_MINE_PATH);
    expect(rows).toHaveLength(1);
    expect(rows[0].cashRegisterId).toBe('reg-2');
  });
});
