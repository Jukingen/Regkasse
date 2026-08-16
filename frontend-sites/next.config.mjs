import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
// npm workspaces hoist `next` to the repo root node_modules — production
// file tracing needs the monorepo root. Dev/build use webpack (`--webpack`);
// no Turbopack config (same Windows RAM issue as frontend-admin).
const monorepoRoot = path.resolve(__dirname, '..');

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  outputFileTracingRoot: monorepoRoot,
};

export default nextConfig;
