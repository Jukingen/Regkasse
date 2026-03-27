/**
 * Ham API hata gövdesinden tek bir metin çıkarır (axios/orval şekli; .NET ProblemDetails uyumlu).
 * Kullanıcıya göstermeden önce `userFacingApiError` ile sarılmalıdır.
 */

function firstValidationMessage(errors: unknown): string | undefined {
  if (errors == null || typeof errors !== 'object') return undefined;
  const obj = errors as Record<string, unknown>;
  for (const key of Object.keys(obj)) {
    const val = obj[key];
    if (Array.isArray(val) && val.length > 0 && typeof val[0] === 'string') return val[0].trim();
    if (typeof val === 'string') return val.trim();
  }
  return undefined;
}

export function extractRawApiErrorMessage(error: unknown): string | undefined {
  if (error == null) return undefined;
  const e = error as {
    response?: {
      data?: {
        message?: string;
        title?: string;
        error?: string;
        detail?: string;
        details?: string;
        reason?: string;
        errors?: unknown;
      };
    };
    message?: string;
  };
  const d = e.response?.data;
  const validation = d?.errors != null ? firstValidationMessage(d.errors) : undefined;
  const s =
    (typeof d?.message === 'string' && d.message.trim()) ||
    validation ||
    (typeof d?.title === 'string' && d.title.trim()) ||
    (typeof d?.error === 'string' && d.error.trim()) ||
    (typeof d?.detail === 'string' && d.detail.trim()) ||
    (typeof d?.details === 'string' && d.details.trim()) ||
    (typeof d?.reason === 'string' && d.reason.trim()) ||
    (typeof e.message === 'string' && e.message.trim());
  return s || undefined;
}

export function extractHttpStatusFromUnknownError(error: unknown): number | undefined {
  const s = (error as { response?: { status?: number } })?.response?.status;
  return typeof s === 'number' && Number.isFinite(s) ? s : undefined;
}
