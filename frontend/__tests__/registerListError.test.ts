import { classifyRegisterListError } from '../utils/registerListError';

describe('classifyRegisterListError', () => {
  it('returns forbidden for HTTP 403', () => {
    expect(classifyRegisterListError({ status: 403 })).toBe('forbidden');
  });

  it('returns unauthorized for HTTP 401', () => {
    expect(classifyRegisterListError({ status: 401 })).toBe('unauthorized');
  });

  it('returns network for axios-style network codes', () => {
    expect(classifyRegisterListError({ code: 'ERR_NETWORK', message: 'x' })).toBe('network');
    expect(classifyRegisterListError({ message: 'Network Error' })).toBe('network');
  });

  it('returns unknown otherwise', () => {
    expect(classifyRegisterListError({ status: 500 })).toBe('unknown');
    expect(classifyRegisterListError(new Error('oops'))).toBe('unknown');
  });
});
