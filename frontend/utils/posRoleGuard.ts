/**
 * POS uygulamasına giriş yetkisi olan roller.
 * Sadece Cashier ve SuperAdmin POS'a erişebilir.
 * Manager şimdilik POS'a giremez; Accountant ve ReportViewer kesinlikle giremez.
 *
 * "Admin" backend'de SuperAdmin'e normalize edilmiş legacy alias'tır
 * (bkz. backend/Auth/RoleLegacyMapping.cs — ["Admin"] = Roles.SuperAdmin).
 * Geçiş dönemi uyumluluğu için burada da kabul edilir.
 */
const POS_ALLOWED_ROLES: ReadonlySet<string> = new Set([
  'Cashier',
  'SuperAdmin',
  'Admin', // Legacy alias → SuperAdmin. Backend migration tamamlanınca kaldırılabilir.
]);

/**
 * Kullanıcının POS uygulamasına erişim yetkisi olup olmadığını kontrol eder.
 * - role null/undefined ise → deny
 * - Çoklu rol varsa (roles[]), herhangi biri allowed ise → allow
 */
export function isPosAllowedRole(
  role: string | null | undefined,
  roles?: string[] | null,
): boolean {
  if (role && POS_ALLOWED_ROLES.has(role)) {
    return true;
  }

  if (roles?.length) {
    return roles.some((r) => POS_ALLOWED_ROLES.has(r));
  }

  return false;
}
