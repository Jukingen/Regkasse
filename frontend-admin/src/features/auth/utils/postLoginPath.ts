import { CHANGE_PASSWORD_PATH } from '@/features/auth/constants/changePasswordRoute';
import { getDefaultLandingPathFromStorage } from '@/lib/personalization/PersonalizationProvider';

/**
 * Post-login destination shared by LoginForm and AuthGate so they cannot
 * race `router.push('/dashboard')` vs a custom landing path.
 * Default personalization landing is `/dashboard`.
 */
export function resolvePostLoginPath(mustChangePassword: boolean): string {
  if (mustChangePassword) {
    return CHANGE_PASSWORD_PATH;
  }
  return getDefaultLandingPathFromStorage();
}

/**
 * Leave the login document with a full navigation. Soft `router.replace` after writing
 * the JWT cookie races `proxy.ts` (`/login` → `/dashboard`) and aborts webpack compile.
 */
export function navigateAfterAuth(path: string): void {
  window.location.assign(path);
}
