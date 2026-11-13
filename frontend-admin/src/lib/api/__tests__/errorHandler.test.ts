import type { AxiosError } from 'axios';
import { afterEach, describe, expect, it, vi } from 'vitest';

import * as notificationService from '@/lib/notificationService';

import {
  ApiTransportErrorKind,
  classifyApiTransportError,
  getApiTransportErrorI18nKey,
  notifyApiTransportError,
  resolveApiTransportErrorMessage,
} from '../errorHandler';

function httpError(status: number): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: `Request failed with status code ${status}`,
    response: { status, data: {}, statusText: '', headers: {}, config: {} as never },
    config: { method: 'get', url: '/api/admin/users' } as never,
    toJSON: () => ({}),
  } as AxiosError;
}

function networkError(): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: 'Network Error',
    code: 'ERR_NETWORK',
    config: { method: 'get', url: '/api/admin/users' } as never,
    toJSON: () => ({}),
  } as AxiosError;
}

describe('api errorHandler', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('classifies 429 / 5xx / network', () => {
    expect(classifyApiTransportError(httpError(429))).toBe(
      ApiTransportErrorKind.RATE_LIMIT_EXCEEDED
    );
    expect(classifyApiTransportError(httpError(500))).toBe(ApiTransportErrorKind.SERVER_ERROR);
    expect(classifyApiTransportError(httpError(503))).toBe(ApiTransportErrorKind.SERVER_ERROR);
    expect(classifyApiTransportError(networkError())).toBe(ApiTransportErrorKind.NETWORK_ERROR);
    expect(classifyApiTransportError(httpError(400))).toBeNull();
    expect(classifyApiTransportError(httpError(401))).toBeNull();
    expect(classifyApiTransportError(httpError(403))).toBeNull();
    expect(classifyApiTransportError(httpError(404))).toBeNull();
  });

  it('maps kinds to i18n keys', () => {
    expect(getApiTransportErrorI18nKey(ApiTransportErrorKind.RATE_LIMIT_EXCEEDED)).toBe(
      'common.errors.http429'
    );
    expect(getApiTransportErrorI18nKey(ApiTransportErrorKind.SERVER_ERROR)).toBe(
      'common.errors.http500'
    );
    expect(getApiTransportErrorI18nKey(ApiTransportErrorKind.NETWORK_ERROR)).toBe(
      'common.errors.network'
    );
  });

  it('resolves localized DE messages from catalog', () => {
    expect(resolveApiTransportErrorMessage(ApiTransportErrorKind.RATE_LIMIT_EXCEEDED, 'de')).toBe(
      'Zu viele Anfragen. Bitte spaeter erneut versuchen.'
    );
    expect(resolveApiTransportErrorMessage(ApiTransportErrorKind.SERVER_ERROR, 'de')).toBe(
      'Serverfehler. Bitte spaeter erneut versuchen.'
    );
    expect(resolveApiTransportErrorMessage(ApiTransportErrorKind.NETWORK_ERROR, 'de')).toBe(
      'Netzwerkfehler. Bitte Verbindung pruefen.'
    );
  });

  it('notifies via notification mode for simulated 429 (after retries exhausted)', () => {
    const notifySpy = vi
      .spyOn(notificationService, 'notifyError')
      .mockImplementation(() => undefined);

    const kind = notifyApiTransportError(httpError(429), {
      url: '/api/admin/users',
      method: 'GET',
      locale: 'de',
    });

    expect(kind).toBe(ApiTransportErrorKind.RATE_LIMIT_EXCEEDED);
    expect(notifySpy).toHaveBeenCalledTimes(1);
    expect(notifySpy).toHaveBeenCalledWith(
      'Zu viele Anfragen. Bitte spaeter erneut versuchen.',
      expect.objectContaining({
        mode: 'notification',
        key: 'api-transport-rate-limit',
      })
    );
  });

  it('does not notify when silent', () => {
    const notifySpy = vi
      .spyOn(notificationService, 'notifyError')
      .mockImplementation(() => undefined);
    expect(notifyApiTransportError(httpError(500), { silent: true })).toBe(
      ApiTransportErrorKind.SERVER_ERROR
    );
    expect(notifySpy).not.toHaveBeenCalled();
  });
});
