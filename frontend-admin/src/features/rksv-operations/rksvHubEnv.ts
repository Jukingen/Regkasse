/**
 * Optional public env for operator-facing TEST/PROD labelling on the RKSV hub.
 * Set `NEXT_PUBLIC_RKSV_ENVIRONMENT` to `TEST` or `PROD` at build time.
 * Türkçe: Operatörler için TEST/PROD görünürlüğü; build zamanında ortam değişkeni.
 */

export type RksvPublicEnvironment = 'TEST' | 'PROD' | 'unknown';

export function readRksvPublicEnvironment(): RksvPublicEnvironment {
  const raw =
    typeof process !== 'undefined' && process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT
      ? String(process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT).trim().toUpperCase()
      : '';
  if (raw === 'TEST' || raw === 'PROD') return raw;
  return 'unknown';
}
