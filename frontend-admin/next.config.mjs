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
    transpilePackages: ['antd', '@ant-design/icons', 'rc-util', 'rc-pagination', 'rc-picker', 'rc-notification', 'rc-tooltip'],
    reactStrictMode: true,
    turbopack: {
        root: path.join(__dirname, '..'),
    },
    experimental: {
        optimizePackageImports: ['antd', '@ant-design/icons'],
    },
    async redirects() {
        return [
            { source: '/sales', destination: '/receipts', permanent: false },
            { source: '/belege', destination: '/receipts', permanent: false },
            { source: '/storno', destination: '/payments/storno-refund-audit', permanent: false },
            { source: '/price-rules', destination: '/pricing-rules', permanent: false },
            { source: '/reporting/operational', destination: '/reporting', permanent: false },
            { source: '/reporting/center', destination: '/reporting/report-center', permanent: false },
            { source: '/reporting/user-activity', destination: '/admin/reports/user-activity', permanent: false },
            {
                source: '/rksv/monatsbeleg',
                destination: '/rksv/sonderbelege?focus=monatsbeleg',
                permanent: false,
            },
            {
                source: '/rksv/jahresbeleg',
                destination: '/rksv/sonderbelege?focus=jahresbeleg',
                permanent: false,
            },
            // Virtual Sonderbeleg menu keys + short aliases → canonical focus panels
            {
                source: '/rksv/sb/startbeleg',
                destination: '/rksv/sonderbelege?focus=startbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/monatsbeleg',
                destination: '/rksv/sonderbelege?focus=monatsbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/monats',
                destination: '/rksv/sonderbelege?focus=monatsbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/jahresbeleg',
                destination: '/rksv/sonderbelege?focus=jahresbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/jahres',
                destination: '/rksv/sonderbelege?focus=jahresbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/nullbeleg',
                destination: '/rksv/sonderbelege?focus=nullbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/schlussbeleg',
                destination: '/rksv/sonderbelege?focus=schlussbeleg',
                permanent: false,
            },
            {
                source: '/rksv/sb/test-helper',
                destination: '/rksv/sonderbelege?focus=test-helper',
                permanent: false,
            },
        ];
    },
};

export default withBundleAnalyzer(nextConfig);
