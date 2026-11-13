import { afterEach, describe, expect, it, vi } from 'vitest';

import * as antdAppBridge from '@/lib/antdAppBridge';
import {
  invokeQueryClientErrorHandler,
  shouldSuppressPublicAuthEntry401Toast,
} from '@/lib/queryErrorHandling';

describe('queryErrorHandling', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

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

  it.each([
    [
      400,
      'Request failed with status code 400',
      'Die Anfrage konnte nicht verarbeitet werden. Bitte prüfen Sie Ihre Eingaben.',
    ],
    [403, 'Request failed with status code 403', 'Sie haben keine Berechtigung für diese Aktion.'],
    [404, 'Request failed with status code 404', 'Der gesuchte Eintrag wurde nicht gefunden.'],
    [
      409,
      'Konflikt (Datenstand).',
      'Dieser Tisch ist von einem anderen Kassierer geöffnet. Bitte schließen Sie ihn zuerst.',
    ],
  ] as const)('toasts HTTP %s as %s', (status, transportMessage, expected) => {
    window.localStorage.setItem('app_language', 'de');
    const errorSpy = vi.spyOn(antdAppBridge, 'showAntdError');
    invokeQueryClientErrorHandler(
      {
        message: transportMessage,
        response: { status, data: { message: transportMessage } },
      },
      { showErrorToast: true }
    );
    expect(errorSpy).toHaveBeenCalledWith(expected);
  });
});
