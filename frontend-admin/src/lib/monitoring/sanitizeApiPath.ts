/**
 * Strip query/hash and collapse UUIDs for safe metric labels (no tokens/PII).
 */
const UUID_RE =
  /[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}/gi;

export function sanitizeApiPath(url: string | undefined | null): string {
  if (!url || typeof url !== 'string') {
    return 'unknown';
  }
  let path = url.trim();
  try {
    if (/^https?:\/\//i.test(path)) {
      path = new URL(path).pathname;
    } else {
      const q = path.indexOf('?');
      const h = path.indexOf('#');
      const cut = Math.min(q === -1 ? path.length : q, h === -1 ? path.length : h);
      path = path.slice(0, cut);
    }
  } catch {
    path = path.split('?')[0]?.split('#')[0] ?? 'unknown';
  }
  path = path.replace(UUID_RE, ':id');
  if (!path.startsWith('/')) {
    path = `/${path}`;
  }
  return path.slice(0, 160) || 'unknown';
}
