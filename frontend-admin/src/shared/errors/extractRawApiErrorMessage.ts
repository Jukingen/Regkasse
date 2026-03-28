/**
 * Ham API hata gövdesinden tek bir metin çıkarır (axios/orval şekli; .NET ProblemDetails uyumlu).
 * Tek kaynak: `normalizeApiError`.
 */
import { normalizeApiError } from './normalizedApiError';

export function extractRawApiErrorMessage(error: unknown): string | undefined {
  return normalizeApiError(error).rawMessage;
}

export function extractHttpStatusFromUnknownError(error: unknown): number | undefined {
  return normalizeApiError(error).httpStatus;
}
