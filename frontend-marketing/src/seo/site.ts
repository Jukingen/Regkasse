export function siteUrl(): string {
  const raw = process.env.NEXT_PUBLIC_SITE_URL?.trim();
  const base = raw && raw.length > 0 ? raw : 'https://regkasse.at';
  return base.replace(/\/+$/, '');
}

export const PUBLIC_PATHS = ['/', '/impressum', '/datenschutz', '/agb'] as const;

export function absoluteUrl(path: string): string {
  return new URL(path, `${siteUrl()}/`).toString();
}
