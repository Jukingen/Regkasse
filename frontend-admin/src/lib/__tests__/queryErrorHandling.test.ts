import { describe, expect, it, vi } from 'vitest';

import * as antdAppBridge from '@/lib/antdAppBridge';
import {
  invokeQueryClientErrorHandler,
  shouldSuppressPublicAuthEntry401Toast,
} from '@/lib/queryErrorHandling';

describe('queryErrorHandling', () => {
  it('suppresses login-entry 401 toasts', () => {
    vi.stubGlobal('window', {
      location: { pathname: '/login' },
    } as Window);

    expect(shouldSuppressPublicAuthEntry401Toast({ response: { status: 401 } })).toBe(true);

    const errorSpy = vi.spyOn(antdAppBridge, 'showAntdError');
    invokeQueryClientErrorHandler({ response: { status: 401 } }, { showErrorToast: true });
    expect(errorSpy).not.toHaveBeenCalled();

    vi.unstubAllGlobals();
  });

  it('shows toast for non-401 when showErrorToast is set', () => {
    vi.stubGlobal('window', {
      location: { pathname: '/login' },
    } as Window);

    const errorSpy = vi.spyOn(antdAppBridge, 'showAntdError');
    invokeQueryClientErrorHandler(new Error('Server unavailable'), { showErrorToast: true });
    expect(errorSpy).toHaveBeenCalledWith('Server unavailable');

    vi.unstubAllGlobals();
  });
});
