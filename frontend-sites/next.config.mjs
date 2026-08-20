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
  experimental: {
    // Cap webpack workers on high-core Windows hosts (same as frontend-admin).
    cpus: 2,
    webpackMemoryOptimizations: true,
    webpackBuildWorker: true,
  },
  webpack: (config, { dev }) => {
    if (dev) {
      config.parallelism = 2;
    }
    return config;
  },
};

export default nextConfig;
