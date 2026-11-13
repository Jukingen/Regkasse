import { afterEach, beforeEach, describe, expect, it, jest } from '@jest/globals';

import deCommon from '../i18n/locales/de/common.json';
import { handleAPIError } from '../services/errorService';
import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';

jest.mock('../i18n', () => {
  const de = require('../i18n/locales/de/common.json') as typeof deCommon;
  return {
    __esModule: true,
    default: {
      t: (key: string) => {
        const messages: Record<string, string> = {
          'common:errors.http400': de.errors.http400,
          'common:errors.http403': de.errors.http403,
          'common:errors.http404': de.errors.http404,
          'common:errors.http409': de.errors.http409,
        };
        return messages[key] ?? key;
      },
    },
  };
});

const HTTP_400 = 'Die Anfrage konnte nicht verarbeitet werden. Bitte prüfen Sie Ihre Eingaben.';
const HTTP_403 = 'Sie haben keine Berechtigung für diese Aktion.';
const HTTP_404 = 'Der gesuchte Eintrag wurde nicht gefunden.';
const HTTP_409 =
  'Dieser Tisch ist von einem anderen Kassierer geöffnet. Bitte schließen Sie ihn zuerst.';

describe('POS user-facing HTTP errors', () => {
  beforeEach(() => {
    jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('replaces axios status 400 with the input-check message', () => {
    expect(deCommon.errors.http400).toBe(HTTP_400);
    expect(
      handleAPIError({
        response: { status: 400 },
        message: 'Request failed with status code 400',
      }).message
    ).toBe(HTTP_400);
    expect(
      readPosApiErrorMessage(
        { message: 'Request failed with status code 400', response: { status: 400 } },
        'fallback'
      )
    ).toBe(HTTP_400);
  });

  it('replaces 403 with the permission message', () => {
    expect(deCommon.errors.http403).toBe(HTTP_403);
    expect(
      handleAPIError({
        response: { status: 403, data: { message: 'Forbidden' } },
        message: 'Request failed with status code 403',
      }).message
    ).toBe(HTTP_403);
  });

  it('replaces 404 with the not-found message', () => {
    expect(deCommon.errors.http404).toBe(HTTP_404);
    expect(
      handleAPIError({
        response: { status: 404 },
        message: 'Request failed with status code 404',
      }).message
    ).toBe(HTTP_404);
  });

  it('replaces Konflikt (Datenstand) with the open-table message', () => {
    expect(deCommon.errors.http409).toBe(HTTP_409);
    expect(
      handleAPIError({
        response: { status: 409, data: { message: 'Konflikt (Datenstand).' } },
        message: 'Request failed with status code 409',
      }).message
    ).toBe(HTTP_409);
  });
});
