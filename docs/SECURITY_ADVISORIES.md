# Accepted npm audit risk (Expo SDK 56)

**Status:** accepted risk. **Last reviewed:** 2026-11-23.

`npm audit` reports **6 high** and **18 moderate** findings on the current lockfile. Critical findings are closed (non-breaking `npm audit fix`, then Orval 8.49.0). The remaining rows are the Expo SDK 56 toolchain plus a few build-only plugins that share the same leaf packages.

Do **not** run `npm audit fix --force` for these rows. Do **not** change the Expo SDK pin to clear them.

## Why `--force` is not applied

`npm audit fix --force` resolves the tree by leaving the declared SDK 56 ranges. The suggested targets include:

| Suggested install | Effect |
|---|---|
| `expo@52.0.49` | Downgrades the POS app from Expo SDK 56 to SDK 52 |
| `expo-router@58.0.32` | Jumps the router onto the SDK 58 line while the rest of the app stays on SDK 56 |
| `@sentry/react-native@8.20.0` | Downgrades the installed 8.32.0 client |
| `@trivago/prettier-plugin-sort-imports@5.2.2` | Downgrades the v6 formatter plugin |
| `babel-plugin-module-resolver@5.0.0` | Downgrades the plugin the POS Babel config uses |

SDK 56.0.24 is the current `sdk-56` dist-tag. No newer SDK 56 patch leaves these advisory ranges. Clearing them is an SDK migration, tracked separately below.

## Remaining advisories

The 24 package rows collapse to four leaf advisories. The other rows (`expo`, `@expo/cli`, `expo-router`, `@expo/metro-config`, `expo-updates`, `expo-sharing`, `expo-splash-screen`, Sentry bundler plugins, and the Prettier/Babel plugins) are parents of these leaves.

| Package | Advisory | Installed copy | When it runs |
|---|---|---|---|
| `zod` &lt; 4.6.6 | [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) — memory exhaustion while validating oversized constrained collections | `zod@3.25.76` inside `@expo/cli@56.1.27` | `expo start`, prebuild, and export on a developer machine or in CI |
| `decode-uri-component` ≤ 0.5.0 | [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr) — denial of service on malformed percent-encoded input | `0.2.2` via `query-string@7.1.3`, declared by `expo-router@56.2.22` | Not imported by the published `expo-router` build or by POS source. Metro does not bundle it into the cashier app |
| `glob` 9.0.0–14.0.1 | [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) — filesystem walk can continue after an `AbortSignal` timeout | `glob@13.0.7` | Expo CLI, Metro/Babel, the `expo-updates` VCS helper, fingerprint, and the Sentry source-map upload plugin |
| `minimatch` 7.0.0–11.0.0 | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249) — denial of service via glob-part amplification | 8.x / 9.x / 10.2.6 under `glob@13`, fingerprint, and the Prettier import plugin | Same Node build tools. Prettier runs in the editor and in format scripts |

`zod@4.6.7` elsewhere in the tree (including Orval) is outside this range. `glob@7.x` and `minimatch@3.x` / `11.0.9` copies are also outside the flagged ranges. Only the copies named above are in scope.

### Why each one is dev-time, not the POS runtime

The installed cashier app is a React Native binary. It does not run Node, Expo CLI, or `zod` schema validation.

- **`zod`.** Only `@expo/cli` loads this copy, while it validates local config and manifests. A huge document can exhaust memory on the machine running the CLI. A register session never calls it.
- **`decode-uri-component`.** `expo-router` lists `query-string`, and the published package does not import it. POS sources do not import it either. The denial-of-service routine stays on disk in `node_modules` and stays out of the cashier bundle.
- **`glob`.** Call sites are Node programs: the Expo CLI, Babel module resolution during bundling, `expo-updates/utils` (the gitignore helper used when publishing updates, not `expo-updates/build`, which the device loads), fingerprint, and `@sentry/bundler-plugins` during a source-map upload. `expo-sharing` and `expo-splash-screen` appear in the audit because their config plugins depend on `@expo/config-plugins`, which is prebuild tooling. The native share and splash implementations do not walk the filesystem with `glob`.
- **`minimatch`.** It is the matcher under those same `glob` calls, plus the Prettier import-sort plugin. A malicious glob pattern has to be passed to the build tool. Cashiers do not pass glob patterns.

Sentry's request-time SDK in the admin app is not the package `npm audit` flags. The moderate `@sentry/nextjs` row is the bundler plugin's `glob` dependency.

A cashier, or a network client of the POS, cannot reach these libraries over HTTP. The realistic case is a developer workstation or CI job fed a hostile config file or glob pattern from the repo. Keep `expo start`, prebuild, EAS, and Sentry source-map upload on developer machines and CI. Do not run them on a register.

## What we are not doing

- No `npm audit fix --force`.
- No Expo SDK version change in `frontend/package.json`.
- No `overrides` that force `glob@14`, `minimatch@11`, or `zod@4` under SDK 56. Those are major bumps inside the CLI the SDK 56 packages call, and they can break `expo start` and prebuild. `decode-uri-component@0.5.1` would be a smaller override and would not change the cashier bundle, because that module is not imported.

There is no POS or app-config switch that unloads these packages. They load from the toolchain, not from tenant settings.

## Future Expo SDK 58 upgrade

Treat closure as a planned SDK migration, not a security hotfix.

- SDK 56.0.24 does not have a later patch that leaves the ranges.
- SDK 57 (`expo@57.0.33`) moves the CLI to `glob@^14` and drops the CLI `zod@3` dependency. `@expo/metro-config@57.0.13` still depends on `glob@^13`, and `expo-router@57` still depends on `query-string@^7`. SDK 57 does not clear this set.
- SDK 58 is the current `latest` tag (`expo@58.0.26` at review time). CLI and Metro use `tinyglobby` instead of `glob@13`, and `expo-router@58.0.32` no longer depends on `query-string`. That is the line that removes the high leaf packages. `@expo/fingerprint` can still pull `minimatch@^10`, and the Prettier and Babel plugins are outside the Expo pin, so a moderate `minimatch` or `glob` row can remain after the upgrade until those packages move.

The SDK 58 move is separate work. It includes the React Native and native-module compatibility pass. Re-run `npm audit` after that upgrade and delete or shrink this acceptance note for any advisory that is gone.
