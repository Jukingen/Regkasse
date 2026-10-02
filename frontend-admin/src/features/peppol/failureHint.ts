const FAILURE_SUFFIXES: Record<string, string> = {
  'peppol-reserved': 'peppol_reserved',
  'peppol-participant-not-configured': 'peppol_participant_not_configured',
  'peppol-live-not-allowed': 'peppol_live_not_allowed',
  'peppol-ack-timeout': 'peppol_ack_timeout',
  'peppol-ack-error': 'peppol_ack_error',
  'storecove-http-400': 'storecove_http_400',
};

/** Suffix for `peppol.submissions.failure.<suffix>`. Hyphens are not valid catalog keys. */
export function failureHintSuffix(code: string | null | undefined): string {
  if (!code) return 'unknown';
  const known = FAILURE_SUFFIXES[code];
  if (known) return known;
  if (/^storecove-http-5\d\d$/.test(code)) return 'storecove_http_5xx';
  return 'unknown';
}
