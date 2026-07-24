/**
 * Redact secrets from technical log payloads before console / Sentry / beacons.
 * Never log passwords, tokens, JWTs, or auth headers in clear text.
 */

const REDACTED = '[REDACTED]';
const REDACTED_JWT = '[REDACTED_JWT]';

/** Case-insensitive key names that must never appear in clear text. */
const SENSITIVE_KEY =
  /^(password|passwd|pwd|secret|token|access[_-]?token|refresh[_-]?token|id[_-]?token|authorization|auth|bearer|cookie|set-cookie|api[_-]?key|x[_-]?api[_-]?key|license[_-]?key|private[_-]?key|client[_-]?secret)$/i;

const JWT_SHAPE = /^eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/;

const MAX_DEPTH = 6;

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value) && !(value instanceof Error);
}

type HttpErrorLike = Error & {
  isAxiosError?: boolean;
  code?: string;
  response?: { status?: number; data?: unknown };
  config?: { url?: string; method?: string; baseURL?: string };
};

function isHttpErrorLike(error: Error): error is HttpErrorLike {
  const e = error as HttpErrorLike;
  return e.isAxiosError === true || e.response != null || e.config != null;
}

/**
 * Shape Error / AxiosError into redacted plain fields for structured logs.
 * Keeps status, endpoint, method, and response data — never auth headers/tokens.
 */
function shapeLoggableError(error: Error, depth: number): Record<string, unknown> {
  const base: Record<string, unknown> = {
    name: error.name,
    message: error.message,
  };

  if (!isHttpErrorLike(error)) {
    return base;
  }

  const method =
    typeof error.config?.method === 'string' ? error.config.method.toUpperCase() : undefined;
  const endpoint =
    typeof error.config?.url === 'string' && error.config.url.trim()
      ? error.config.url.trim()
      : undefined;

  return redactTechnicalLogArg(
    {
      ...base,
      error: error.message,
      status: typeof error.response?.status === 'number' ? error.response.status : null,
      data: error.response?.data ?? null,
      endpoint: endpoint ?? null,
      method: method ?? null,
      code: typeof error.code === 'string' ? error.code : null,
    },
    depth + 1
  ) as Record<string, unknown>;
}

export function redactTechnicalLogArg(value: unknown, depth = 0): unknown {
  if (depth > MAX_DEPTH) {
    return '[MaxDepth]';
  }

  if (typeof value === 'string') {
    if (JWT_SHAPE.test(value.trim())) {
      return REDACTED_JWT;
    }
    return value;
  }

  if (value instanceof Error) {
    return shapeLoggableError(value, depth);
  }

  if (Array.isArray(value)) {
    return value.map((item) => redactTechnicalLogArg(item, depth + 1));
  }

  if (!isPlainObject(value)) {
    return value;
  }

  const out: Record<string, unknown> = {};
  for (const [key, nested] of Object.entries(value)) {
    if (SENSITIVE_KEY.test(key)) {
      out[key] = REDACTED;
    } else {
      out[key] = redactTechnicalLogArg(nested, depth + 1);
    }
  }
  return out;
}

export function redactLogArgs(args: unknown[]): unknown[] {
  return args.map((arg) => redactTechnicalLogArg(arg));
}
