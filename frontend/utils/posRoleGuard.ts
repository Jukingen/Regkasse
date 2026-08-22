/**
 * POS uygulamasına giriş yetkisi olan roller.
 * Cashier, Waiter ve SuperAdmin POS'a erişebilir.
 */
const POS_ALLOWED_ROLES: ReadonlySet<string> = new Set(['cashier', 'waiter', 'superadmin']);

function canonicalRole(role: string | null | undefined): string {
  return (role ?? '').trim().toLowerCase();
}

/**
 * Kullanıcının POS uygulamasına erişim yetkisi olup olmadığını kontrol eder.
 * - role null/undefined ise → deny
 * - Çoklu rol varsa (roles[]), herhangi biri allowed ise → allow
 * Backend ClientAppPolicy ile aynı şekilde case-insensitive.
 */
export function isPosAllowedRole(
  role: string | null | undefined,
  roles?: string[] | null
): boolean {
  if (POS_ALLOWED_ROLES.has(canonicalRole(role))) {
    return true;
  }

  if (roles?.length) {
    return roles.some((r) => POS_ALLOWED_ROLES.has(canonicalRole(r)));
  }

  return false;
}
