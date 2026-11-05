/** Shown under Benutzer. Prefer the stored address; otherwise username@mandant. */
export function adminUserDisplayEmail(
  email: string | null | undefined,
  userName: string | null | undefined,
  tenantSlug: string | null | undefined
): string {
  const stored = email?.trim();
  if (stored && stored.includes('@')) return stored;

  const name = userName?.trim();
  if (name && name.includes('@')) return name;

  const slug = tenantSlug?.trim().toLowerCase();
  if (name && slug) return `${name}@${slug}.regkasse.at`;

  return stored || name || '—';
}

export function sameTenantId(left: string | null | undefined, right: string | null | undefined): boolean {
  const a = left?.trim().toLowerCase() ?? '';
  const b = right?.trim().toLowerCase() ?? '';
  return a.length > 0 && a === b;
}

export function sameRole(left: string | null | undefined, right: string | null | undefined): boolean {
  const a = left?.trim().toLowerCase() ?? '';
  const b = right?.trim().toLowerCase() ?? '';
  return a.length > 0 && a === b;
}
