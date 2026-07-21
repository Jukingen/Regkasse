/**
 * technicalConsole yükü — anahtarlar İngilizce; değerler tanısal.
 */
import type { NormalizedApiError } from './normalizedApiError';

export function buildTechnicalApiErrorPayload(
  normalized: NormalizedApiError
): Record<string, unknown> {
  return {
    httpStatus: normalized.httpStatus ?? null,
    code: normalized.code ?? null,
    severity: normalized.severity ?? null,
    retryable: normalized.retryable ?? null,
    traceId: normalized.traceId ?? null,
    rawMessage: normalized.rawMessage ?? null,
    fieldErrorKeys: normalized.fieldErrors ? Object.keys(normalized.fieldErrors) : [],
    remediationHint: normalized.remediationHint ?? null,
  };
}
