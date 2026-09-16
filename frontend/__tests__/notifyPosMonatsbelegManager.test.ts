import { describe, expect, it, jest, beforeEach } from '@jest/globals';

import {
  POS_MONATSBELEG_NOTIFY_MANAGER_PATH,
  notifyPosMonatsbelegManager,
} from '../services/api/cashRegisterService';
import { apiClient } from '../services/api/config';

jest.mock('../services/api/config', () => ({
  apiClient: {
    post: jest.fn(),
  },
}));

describe('notifyPosMonatsbelegManager', () => {
  beforeEach(() => {
    jest.mocked(apiClient.post).mockReset();
  });

  it('posts the cash register id to the POS notify-manager path', async () => {
    jest.mocked(apiClient.post).mockResolvedValue({
      ok: true,
      code: 'OK',
      message: 'Mandanten-Admin was notified.',
    });
    const id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const result = await notifyPosMonatsbelegManager(id);
    expect(apiClient.post).toHaveBeenCalledWith(POS_MONATSBELEG_NOTIFY_MANAGER_PATH, {
      cashRegisterId: id,
    });
    expect(result).toEqual({
      ok: true,
      code: 'OK',
      message: 'Mandanten-Admin was notified.',
    });
  });
});
