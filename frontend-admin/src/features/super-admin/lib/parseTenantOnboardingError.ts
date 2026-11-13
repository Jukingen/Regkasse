import { normalizeApiError } from '@/shared/errors/normalizedApiError';

export type TenantOnboardingError = {
  message: string;
  code?: string;
  slugSuggestions: string[];
};

function pickSuggestions(data: Record<string, unknown> | undefined): string[] {
  if (!data) {
    return [];
  }
  const raw = data.suggestions;
  if (!Array.isArray(raw)) {
    return [];
  }
  return raw
    .filter((s): s is string => typeof s === 'string' && s.trim().length > 0)
    .map((s) => s.trim());
}

/** Parses POST /api/admin/tenants failure bodies (message, code, suggestions). */
export function parseTenantOnboardingError(
  error: unknown,
  fallbackMessage: string
): TenantOnboardingError {
  const normalized = normalizeApiError(error);
  const axiosData = (error as { response?: { data?: unknown } })?.response?.data;
  const data =
    axiosData != null && typeof axiosData === 'object' && !Array.isArray(axiosData)
      ? (axiosData as Record<string, unknown>)
      : undefined;

  return {
    message: normalized.rawMessage ?? fallbackMessage,
    code: normalized.code ?? (typeof data?.code === 'string' ? data.code : undefined),
    slugSuggestions: pickSuggestions(data),
  };
}
