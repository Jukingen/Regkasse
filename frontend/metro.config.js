const { getDefaultConfig } = require('expo/metro-config');

/**
 * Expo SDK 56 Metro config.
 *
 * - Prefer `expo/metro-config` defaults (sourceExts already include ts/tsx/mjs/cjs).
 * - Do not set monorepo-wide `watchFolders` / `nodeModulesPaths` — SDK 52+ configures
 *   those automatically; SDK 56 on-demand filesystem resolves out-of-root links without
 *   watching the entire repo.
 * - Cap workers to match `npm start --max-workers=2` (stable on constrained machines).
 */
const projectRoot = __dirname;
const config = getDefaultConfig(projectRoot);

config.maxWorkers = 2;

// Explicit project root only (no parent monorepo crawl). Add sibling package roots here
// if the app starts importing them via Metro (today POS code stays under frontend/).
config.watchFolders = [projectRoot];

/** Escape a filesystem path for use inside a RegExp (Windows-safe). */
function escapePathForRegex(filePath) {
  return filePath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

// Only block project-local trees — never `node_modules/**/dist` (breaks react-native-web etc.).
const projectLocalBlock = new RegExp(
  `^${escapePathForRegex(projectRoot)}[/\\\\](dist|archive|examples|coverage)([/\\\\]|$)`
);

const defaultBlockList = config.resolver.blockList;
config.resolver.blockList = [
  ...(Array.isArray(defaultBlockList)
    ? defaultBlockList
    : defaultBlockList
      ? [defaultBlockList]
      : []),
  projectLocalBlock,
];

// Keep Error / Hermes stack frames readable in production minify.
config.transformer.minifierConfig = {
  ...config.transformer.minifierConfig,
  keep_fnames: true,
  mangle: {
    ...(config.transformer.minifierConfig?.mangle ?? {}),
    keep_fnames: true,
  },
};

module.exports = config;
