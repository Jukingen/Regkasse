export function isLiveE2E(): boolean {
  return process.env.E2E_LIVE === '1' || process.env.E2E_LIVE === 'true';
}

export function e2eCredentials() {
  return {
    loginIdentifier: process.env.E2E_ADMIN_LOGIN ?? 'admin@admin.com',
    password: process.env.E2E_ADMIN_PASSWORD ?? 'Admin123!',
  };
}
