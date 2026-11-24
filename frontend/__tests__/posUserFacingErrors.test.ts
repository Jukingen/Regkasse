import { afterEach, beforeEach, describe, expect, it, jest } from '@jest/globals';
import fs from 'fs';
import path from 'path';

import { changeLanguage } from '../i18n';
import deCheckout from '../i18n/locales/de/checkout.json';
import deCommon from '../i18n/locales/de/common.json';
import { ErrorMessages, handleAPIError } from '../services/errorService';
import { isPaymentTransportFailure } from '../utils/isPaymentTransportFailure';
import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';
import { resolvePosHttpErrorMessage } from '../utils/userFacingHttpError';

const ENGLISH_HINTS = /\b(please|failed|error|try again|unavailable|gateway|network)\b/i;

describe('POS German, actionable error messages', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('turns a missing response into a network message with a next step', () => {
    expect(handleAPIError({ code: 'ERR_NETWORK', message: 'Network Error' }).message).toBe(
      deCommon.errors.network
    );
    expect(deCommon.errors.network).toContain('prüfen');
  });

  it('turns a client timeout into a timeout message', () => {
    expect(
      handleAPIError({ code: 'ECONNABORTED', message: 'timeout of 30000ms exceeded' }).message
    ).toBe(deCommon.errors.timeout);
  });

  it('maps 401, 422 and 5xx to German text', () => {
    expect(handleAPIError({ response: { status: 401 } }).message).toBe(deCommon.errors.http401);
    expect(handleAPIError({ response: { status: 422 } }).message).toBe(deCommon.errors.http422);
    for (const status of [500, 502, 503, 504]) {
      expect(handleAPIError({ response: { status } }).message).toBe(deCommon.errors.http5xx);
    }
  });

  it('keeps a real 422 server message', () => {
    expect(
      handleAPIError({ response: { status: 422, data: { message: 'Betrag fehlt.' } } }).message
    ).toBe('Betrag fehlt.');
  });

  it('never shows raw axios status lines for 429 and 5xx', () => {
    const axiosLine = (status: number) => `Request failed with status code ${status}`;
    expect(resolvePosHttpErrorMessage(429, undefined, axiosLine(429))).toBe(deCommon.errors.http429);
    expect(resolvePosHttpErrorMessage(500, undefined, axiosLine(500))).toBe(deCommon.errors.http5xx);
    expect(
      readPosApiErrorMessage(
        { message: axiosLine(503), response: { status: 503 } },
        'fallback'
      )
    ).toBe(deCommon.errors.http5xx);
  });

  it('keeps an explicit server message for 5xx', () => {
    expect(resolvePosHttpErrorMessage(503, 'TSE nicht erreichbar.', undefined)).toBe(
      'TSE nicht erreichbar.'
    );
  });

  it('has no English text in the cashier-facing catalogs', () => {
    const texts = [
      ...Object.values(deCommon.errors),
      ...Object.values(deCheckout.posFlow.payment.guards),
      ...Object.values(ErrorMessages),
    ];
    for (const text of texts) {
      expect(text).not.toMatch(ENGLISH_HINTS);
    }
  });

  it('does not make the offline-queue logic treat an HTTP failure as a dropped connection', () => {
    for (const message of [
      deCommon.errors.http401,
      deCommon.errors.http422,
      deCommon.errors.http429,
      deCommon.errors.http5xx,
    ]) {
      expect(isPaymentTransportFailure({ status: 500, message })).toBe(false);
    }
  });

  it('replaces every Debug Error alert in the payment modal with a guard message', () => {
    const source = fs.readFileSync(
      path.join(__dirname, '../components/PaymentModal.tsx'),
      'utf8'
    );
    expect(source).not.toContain("'Debug Error'");
    expect(source).not.toContain('Failed at:');
    for (const key of Object.keys(deCheckout.posFlow.payment.guards)) {
      expect(source).toContain(`checkout:posFlow.payment.guards.${key}`);
    }
  });
});
