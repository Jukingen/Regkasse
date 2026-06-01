/**
 * RKSV / FinanzOnline: üretim derlemesinde ortam etiketi zorunlu (operatör tarafında TEST|PROD ayrımı).
 * Türkçe: Eksik veya geçersiz NEXT_PUBLIC_RKSV_ENVIRONMENT ile sessiz üretim artefaktı üretilmez.
 */
import bundleAnalyzer from '@next/bundle-analyzer';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** UTF-8 BOM; `rksvEnvironment.ts` ile aynı kural (editör .env başına FEFF ekleyebilir). */
function stripBomAndTrimRksvEnv(raw) {
    if (raw === undefined || raw === null) return '';
    return String(raw).replace(/^\uFEFF/, '').trim();
}

function assertRksvPublicEnvironmentForProductionBuild() {
    if (!process.argv.includes('build')) return;
    const raw = process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT;
    const trimmed = stripBomAndTrimRksvEnv(raw);
    const normalized = trimmed.toUpperCase();
    if (normalized === 'TEST' || normalized === 'PROD') return;
    const display =
        raw === undefined || raw === null || trimmed === ''
            ? '(unset or empty)'
            : JSON.stringify(trimmed);
    throw new Error(
        `[regkasse-admin] RKSV / FinanzOnline: NEXT_PUBLIC_RKSV_ENVIRONMENT must be TEST or PROD for ` +
            '`next build` (operator label on /rksv; Registrierkasse context). Got: ' +
            `${display}. See .env.example and README.`
    );
}

assertRksvPublicEnvironmentForProductionBuild();

const withBundleAnalyzer = bundleAnalyzer({
    enabled: process.env.ANALYZE === 'true',
});

/** @type {import('next').NextConfig} */
const nextConfig = {
    allowedDevOrigins: ['admin.regkasse.local', '*.regkasse.local'],
    transpilePackages: ['@ant-design/icons', 'antd', 'rc-util', 'rc-pagination', 'rc-picker', 'rc-notification', 'rc-tooltip'],
    reactStrictMode: true,
    turbopack: {
        root: path.join(__dirname, '..'),
    },
    experimental: {
        optimizePackageImports: ['antd', '@ant-design/icons'],
    },
};

export default withBundleAnalyzer(nextConfig);
