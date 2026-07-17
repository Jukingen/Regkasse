/** Dedicated public route for mandatory first-login / temporary password change. */
export const CHANGE_PASSWORD_PATH = '/force-password-change';

/** Protected self-service route for voluntary password change (header menu, settings). */
export const VOLUNTARY_CHANGE_PASSWORD_PATH = '/settings/password';

export function isChangePasswordPath(pathname: string | null | undefined): boolean {
    if (!pathname) return false;
    const normalized = pathname.replace(/\/$/, '') || '/';
    return normalized === CHANGE_PASSWORD_PATH;
}
