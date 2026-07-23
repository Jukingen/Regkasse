import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Keep Turbopack rooted on this package when the monorepo has a root package-lock.json.
  turbopack: {
    root: __dirname,
  },
};

export default nextConfig;
