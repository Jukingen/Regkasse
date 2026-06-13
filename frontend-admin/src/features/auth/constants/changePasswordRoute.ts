/** Dedicated public route for mandatory first-login password change. */
export const CHANGE_PASSWORD_PATH = '/force-password-change';

export function isChangePasswordPath(pathname: string | null | undefined): boolean {
    if (!pathname) return false;
    const normalized = pathname.replace(/\/$/, '') || '/';
    return normalized === CHANGE_PASSWORD_PATH;
}
