/**
 * Technical and developer diagnostics for the admin app.
 *
 * Policy: all human-readable log lines written here MUST be English. Never pass i18n `t(...)`
 * strings or operator-facing copy into these methods — use Ant Design `message` / `notification`
 * for user-visible UI.
 *
 * Server error bodies (e.g. API `message` fields) may still appear as logged object payloads;
 * that is raw backend content, not frontend-authored diagnostics.
 */

const isDev = process.env.NODE_ENV !== 'production';

const PREFIX = '[regkasse-admin]';

export const technicalConsole = {
  /** Development-only informational trace (auth flow, query init, etc.). */
  devLog(...args: unknown[]) {
    if (isDev) console.log(PREFIX, ...args);
  },

  /** Development-only verbose trace (HTTP debug, token attach, etc.). */
  devDebug(...args: unknown[]) {
    if (isDev) console.debug(PREFIX, ...args);
  },

  /** Warnings (missing keys, legacy routes, parse issues). English-only message strings. */
  warn(...args: unknown[]) {
    console.warn(PREFIX, ...args);
  },

  /** Errors (logout failure, export failure, API error summary). English-only message strings. */
  error(...args: unknown[]) {
    console.error(PREFIX, ...args);
  },
};
