const DROPPED = new Set(['email', 'password', 'token', 'vouchercode', 'tsesignature']);

function isDropped(key: string): boolean {
  return DROPPED.has(key.trim().toLowerCase());
}

function scrubRecord(record: Record<string, unknown> | undefined): Record<string, unknown> | undefined {
  if (!record) return record;
  const next: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(record)) {
    if (isDropped(key)) continue;
    next[key] = value && typeof value === 'object' && !Array.isArray(value)
      ? scrubRecord(value as Record<string, unknown>)
      : value;
  }
  return next;
}

/** Drop the same secret fields the API scrubber removes. Keep userId and tenantId. */
export function scrubPosSentryEvent(event: Record<string, unknown>): Record<string, unknown> {
  const extra = event.extra;
  if (extra && typeof extra === 'object') {
    event.extra = scrubRecord(extra as Record<string, unknown>);
  }
  const user = event.user;
  if (user && typeof user === 'object') {
    const copy = { ...(user as Record<string, unknown>) };
    delete copy.email;
    event.user = copy;
  }
  const request = event.request;
  if (request && typeof request === 'object') {
    const copy = { ...(request as Record<string, unknown>) };
    if (copy.data && typeof copy.data === 'object') {
      copy.data = scrubRecord(copy.data as Record<string, unknown>);
    }
    if (copy.headers && typeof copy.headers === 'object') {
      copy.headers = scrubRecord(copy.headers as Record<string, unknown>);
    }
    event.request = copy;
  }
  return event;
}
