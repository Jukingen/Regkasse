# Accepted npm audit risk (Expo SDK 57)

**Status:** accepted risk. **Last reviewed:** 2026-11-24.

`npm audit` reports **3 high** and **10 moderate** findings on the current lockfile (13 total). Critical findings stay closed. The Expo SDK 57 upgrade removed the CLI `zod@3` high advisory and the Metro `glob@13` copies. What remains is dev-time only.

Do **not** run `npm audit fix --force` for these rows. Do **not** jump the POS app to Expo SDK 58 to clear them in this change.

## Why `--force` is not applied

`npm audit fix --force` leaves the declared SDK 57 ranges. The suggested targets include:

| Suggested install | Effect |
|---|---|
| `expo@52.0.49` | Downgrades the POS app from Expo SDK 57 to SDK 52 |
| `expo-router@58.0.32` | Jumps the router onto the SDK 58 line while the rest of the app stays on SDK 57 |
| `@sentry/react-native@8.20.0` | Downgrades the installed 8.32.0 client |
| `@trivago/prettier-plugin-sort-imports@5.2.2` | Downgrades the v6 formatter plugin |
| `babel-plugin-module-resolver@5.0.0` | Downgrades the plugin the POS Babel config uses |

SDK 57 keeps `@sentry/react-native` at `8.32.0`. Expo's bundled native-module list still names `~7.11.0`. That downgrade is excluded in `frontend/package.json` (`expo.install.exclude`) so the POS Sentry client stays on 8.x.

## Remaining advisories

The 13 package rows collapse to three leaf advisories. `expo-router` and `query-string` are parents of `decode-uri-component`. `expo`, `@expo/fingerprint`, the Sentry bundler plugins, `@trivago/prettier-plugin-sort-imports`, and `babel-plugin-module-resolver` are parents of `glob` / `minimatch`.

| Package | Advisory | Installed copy | When it runs |
|---|---|---|---|
| `decode-uri-component` ≤ 0.5.0 | [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr) — denial of service on malformed percent-encoded input | `0.2.2` via `query-string@7.1.3`, declared by `expo-router@57` | Not imported by the published `expo-router` build or by POS source. Metro does not bundle it into the cashier app |
| `glob` 9.0.0–14.0.1 | [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) — filesystem walk can continue after an `AbortSignal` timeout | Remaining `glob@13` copies under fingerprint, Babel module resolution, and Sentry source-map upload | Node build tools on a developer machine or in CI |
| `minimatch` 7.0.0–11.0.0 | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249) — denial of service via glob-part amplification | Copies under those `glob@13` parents and the Prettier import plugin | Same Node build tools. Prettier runs in the editor and in format scripts |

The SDK 56 CLI copy `zod@3.25.76` is gone. `@expo/cli` on SDK 57 does not depend on it. `zod@4.6.7` elsewhere in the tree is outside the advisory range.

### Why each one is dev-time, not the POS runtime

The installed cashier app is a React Native binary. It does not run Node, Expo CLI, or these matchers.

- **`decode-uri-component`.** `expo-router@57` still lists `query-string@^7.1.3`, and the published package does not import it. POS sources do not import it either. The denial-of-service routine stays on disk in `node_modules` and stays out of the cashier bundle.
- **`glob` / `minimatch`.** Call sites that remain are Node programs: Babel module resolution during bundling, `@expo/fingerprint`, `@sentry/bundler-plugins` during a source-map upload, and the Prettier import-sort plugin. A cashier, or a network client of the POS, cannot reach these libraries over HTTP.

Sentry's request-time SDK in the admin app is not the package `npm audit` flags. The moderate `@sentry/nextjs` row is the bundler plugin's `glob` dependency.

## What we are not doing

- No `npm audit fix --force`.
- No Expo SDK 58 upgrade in this change.
- No `overrides` that force `glob@14`, `minimatch@11`, or `decode-uri-component@0.5.1` under SDK 57. The decode-uri override would not change the cashier bundle, because that module is not imported. The glob and minimatch overrides can break fingerprint, Babel, and the Sentry upload plugin.

## Future Expo SDK 58 upgrade

Treat closure of the remaining high row as a planned SDK migration, not a security hotfix.

- SDK 57 removes the CLI `zod` high advisory and the Metro `glob@13` dependency. It does not remove `expo-router`'s `query-string@^7` dependency, so the high `decode-uri-component` row stays.
- SDK 58 is the current `latest` tag. `expo-router@58.0.32` no longer depends on `query-string`, which is the fix npm suggests for the high rows. `@expo/fingerprint` and the Prettier / Babel plugins sit outside that pin, so a moderate `minimatch` or `glob` row can remain after that upgrade.

The SDK 58 move is separate work. Re-run `npm audit` after it and shrink this note for any advisory that is gone.
