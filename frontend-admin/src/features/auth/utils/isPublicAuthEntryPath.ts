/** Login and related public auth screens — unauthenticated 401 is expected. */
export function isPublicAuthEntryPath(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }
  const path = window.location.pathname;
  return path === '/login' || path.startsWith('/login/');
}
