import { resolveCashierIdForPayment } from '../utils/paymentSessionUser';
import { storage } from '../utils/storage';

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(),
  },
}));

const mockGetItem = storage.getItem as jest.MockedFunction<typeof storage.getItem>;

describe('resolveCashierIdForPayment', () => {
  beforeEach(() => {
    mockGetItem.mockReset();
  });

  it('returns UNKNOWN when no token and no auth user', async () => {
    mockGetItem.mockResolvedValue(null);
    await expect(resolveCashierIdForPayment(null)).resolves.toBe('UNKNOWN');
  });

  it('uses auth user when no token', async () => {
    mockGetItem.mockResolvedValue(null);
    await expect(resolveCashierIdForPayment('  user-1  ')).resolves.toBe('user-1');
  });

  it('prefers JWT sub over mismatched auth user', async () => {
    const b64url = (o: object) =>
      Buffer.from(JSON.stringify(o))
        .toString('base64')
        .replace(/=/g, '')
        .replace(/\+/g, '-')
        .replace(/\//g, '_');
    const token = `${b64url({ alg: 'none', typ: 'JWT' })}.${b64url({
      sub: 'jwt-user',
      exp: Math.floor(Date.now() / 1000) + 3600,
    })}.x`;
    mockGetItem.mockResolvedValue(token);
    await expect(resolveCashierIdForPayment('other-user')).resolves.toBe('jwt-user');
  });
});
