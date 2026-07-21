/**
 * Shared RKSV public-env gate for `next build`.
 * Imported by `next.config.mjs` so production artefacts never ship with UNCONFIGURED/INVALID labels.
 */

export const RKSV_PUBLIC_ENV_VAR_NAME = 'NEXT_PUBLIC_RKSV_ENVIRONMENT';
export const RKSV_ENV_ACCEPTED_PUBLIC = ['TEST', 'PROD'];

/** UTF-8 BOM; keep in sync with `src/shared/config/rksvEnvironment.ts`. */
export function stripBomAndTrimRksvEnv(raw) {
  if (raw === undefined || raw === null) return '';
  return String(raw)
    .replace(/^\uFEFF/, '')
    .trim();
}

/**
 * @param {{ argv?: string[]; envValue?: string | undefined | null }} [options]
 * @returns {void}
 */
export function assertRksvPublicEnvironmentForProductionBuild(options = {}) {
  const argv = options.argv ?? process.argv;
  if (!argv.includes('build')) return;

  const raw =
    options.envValue !== undefined ? options.envValue : process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT;
  const trimmed = stripBomAndTrimRksvEnv(raw);
  const normalized = trimmed.toUpperCase();
  if (normalized === 'TEST' || normalized === 'PROD') return;

  const display =
    raw === undefined || raw === null || trimmed === ''
      ? '(unset or empty)'
      : JSON.stringify(trimmed);
  throw new Error(
    `[regkasse-admin] RKSV / FinanzOnline: ${RKSV_PUBLIC_ENV_VAR_NAME} must be TEST or PROD for ` +
      '`next build` (operator label on /rksv; Registrierkasse context). Got: ' +
      `${display}. See .env.example and README.`
  );
}
