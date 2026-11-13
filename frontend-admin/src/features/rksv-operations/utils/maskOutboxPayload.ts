const SENSITIVE_KEY = /(signature|credential|password|pin|secret|token|jws|session)/i;

/** Masks TSE signatures, participant credentials, and similar secrets before display. */
export function maskOutboxPayload(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((item) => maskOutboxPayload(item));
  }
  if (value && typeof value === 'object') {
    const masked: Record<string, unknown> = {};
    for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
      masked[key] = SENSITIVE_KEY.test(key) ? '***' : maskOutboxPayload(child);
    }
    return masked;
  }
  if (typeof value === 'string' && (value.includes('eyJ') || value.includes('_R1-AT'))) {
    return '***';
  }
  return value;
}
