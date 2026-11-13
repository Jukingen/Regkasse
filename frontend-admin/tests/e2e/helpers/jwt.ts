/** Unsigned JWT for Edge proxy cookie checks (signature is not verified client-side). */
export function makeE2eJwt(
  claims: Record<string, unknown> = {},
  expiresInSeconds = 60 * 60
): string {
  const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url');
  const payload = Buffer.from(
    JSON.stringify({
      sub: 'e2e-super-admin',
      role: 'SuperAdmin',
      tenant_id: '22222222-2222-4222-8222-222222222222',
      tenant_slug: 'dev',
      exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
      ...claims,
    })
  ).toString('base64url');
  return `${header}.${payload}.e2e`;
}
