# Accepted npm audit risk (Expo SDK 58)

**Status:** accepted risk. **Last reviewed:** 2026-11-27.

`npm audit` reports **0 high** and **26 moderate** findings on the current lockfile (26 total). Critical and high findings are closed. The Expo SDK 58 upgrade removed the last high advisory (`decode-uri-component`, pulled in through `expo-router`'s `query-string`). What remains is dev-time only.

Do **not** run `npm audit fix --force` for these rows.

## Why `--force` is not applied

`npm audit fix --force` leaves the declared SDK 58 ranges. The suggested targets include:

| Suggested install | Effect |
|---|---|
| `expo@52.0.49` | Downgrades the POS app from Expo SDK 58 to SDK 52 |
| `@sentry/react-native@8.20.0` | Downgrades the installed 8.32.0 client |
| `babel-plugin-module-resolver@5.0.0` | Downgrades the plugin the POS Babel config uses |
| `jest@29.7.0` | Downgrades the POS test runner from jest 30 |

SDK 58 keeps `@sentry/react-native` at `8.32.0`. Expo's bundled native-module list names `~8.28.0`. The `expo.install.exclude` entry in `frontend/package.json` keeps `expo install` from pinning the POS Sentry client back to that version.

## Remaining advisories

The 26 package rows collapse to two leaf advisories. `expo`, `@expo/fingerprint`, the Sentry bundler plugins (`@sentry/bundler-plugins`, `@sentry/webpack-plugin`, `@sentry/nextjs`, `@sentry/react-native`), `@trivago/prettier-plugin-sort-imports`, `babel-plugin-module-resolver`, and the jest chain (`jest`, `jest-config`, `babel-plugin-istanbul`, `test-exclude`) are parents of `glob` / `minimatch`.

| Package | Advisory | Installed copy | When it runs |
|---|---|---|---|
| `glob` 9.0.0–14.0.1 | [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) — filesystem walk can continue after an `AbortSignal` timeout | `glob@13.0.7` under fingerprint, Babel module resolution, Sentry source-map upload, and jest | Node build and test tools on a developer machine or in CI |
| `minimatch` 7.0.0–11.0.0 | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249) — denial of service via glob-part amplification | `minimatch@9.0.9` and copies under those `glob` parents and the Prettier import plugin | Same Node tools. Prettier runs in the editor and in format scripts |

`decode-uri-component` and `query-string` are no longer in the tree (`npm ls` finds neither).

### Why each one is dev-time, not the POS runtime

The installed cashier app is a React Native binary. It does not run Node, Expo CLI, jest, or these matchers.

- **`glob` / `minimatch`.** Call sites that remain are Node programs: Babel module resolution during bundling, `@expo/fingerprint`, `@sentry/bundler-plugins` during a source-map upload, jest coverage tooling, and the Prettier import-sort plugin. A cashier, or a network client of the POS, cannot reach these libraries over HTTP.

Sentry's request-time SDK in the admin app is not the package `npm audit` flags. The moderate `@sentry/nextjs` row is the bundler plugin's `glob` dependency.

## What we are not doing

- No `npm audit fix --force`.
- No `overrides` that force `glob@14` or `minimatch@11`. They can break fingerprint, Babel, the Sentry upload plugin, and the jest toolchain.

## Closing the remaining rows

Both leaf advisories sit in dev-time tools, so closing them is a tooling upgrade, not a security hotfix. Re-run `npm audit` after any upgrade of those parents and shrink this note for any advisory that is gone.
