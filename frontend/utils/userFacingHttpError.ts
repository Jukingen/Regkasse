import i18n from '../i18n';

const AXIOS_STATUS_MESSAGE = /^Request failed with status code (\d{3})$/i;
const GENERIC_DATA_CONFLICT = /^Konflikt \(Datenstand\)\.?$/;

export function isAxiosStatusMessage(message: string): boolean {
  return AXIOS_STATUS_MESSAGE.test(message.trim());
}

/** Server text that is safe to show. Drops axios status lines and the old data-conflict label. */
export function usableApiMessage(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined;
  const trimmed = value.trim();
  if (!trimmed || isAxiosStatusMessage(trimmed) || GENERIC_DATA_CONFLICT.test(trimmed)) {
    return undefined;
  }
  return trimmed;
}

export function userFacingPosHttpMessage(status: number): string | undefined {
  switch (status) {
    case 400:
      return i18n.t('common:errors.http400');
    case 403:
      return i18n.t('common:errors.http403');
    case 404:
      return i18n.t('common:errors.http404');
    case 409:
      return i18n.t('common:errors.http409');
    default:
      return undefined;
  }
}

/**
 * Cashier-facing text for HTTP 400/403/404/409.
 * 403 and 404 always use the catalog sentence. 400 and 409 keep a real server message.
 */
export function resolvePosHttpErrorMessage(
  status: number | undefined,
  bodyMessage: unknown,
  transportMessage: unknown
): string | undefined {
  if (status === 403 || status === 404) {
    return userFacingPosHttpMessage(status);
  }

  const body = usableApiMessage(bodyMessage);
  const transport = usableApiMessage(transportMessage);
  if (status === 400 || status === 409) {
    return body ?? transport ?? userFacingPosHttpMessage(status);
  }

  return body ?? transport;
}
