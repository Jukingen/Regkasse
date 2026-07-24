#!/usr/bin/env node
/**
 * Generates ai/training/regkasse-ai-dataset.json from the monorepo.
 *
 * - Inventories services, controllers, FA features (paths only — no secrets)
 * - Indexes docs/ + README files + API contract paths
 * - Merges curated patterns from ai/training/curated-patterns.json
 * - Extracts non-secret config section keys from appsettings*.example.json
 *
 * Usage (repo root): node scripts/generate-ai-training-dataset.mjs
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const OUT = path.join(ROOT, 'ai', 'training', 'regkasse-ai-dataset.json');
const CURATED = path.join(ROOT, 'ai', 'training', 'curated-patterns.json');

const SKIP_DIR = new Set([
  'node_modules',
  'bin',
  'obj',
  '.git',
  '.next',
  'dist',
  'coverage',
  '_maint_build_out',
  '.expo',
]);

function walkFiles(dir, predicate, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    if (SKIP_DIR.has(ent.name)) continue;
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) walkFiles(full, predicate, acc);
    else if (predicate(full, ent.name)) acc.push(full);
  }
  return acc;
}

function rel(p) {
  return path.relative(ROOT, p).split(path.sep).join('/');
}

function readUtf8(p) {
  return fs.readFileSync(p, 'utf8');
}

function firstHeading(md) {
  const m = md.match(/^#\s+(.+)$/m);
  return m ? m[1].trim() : null;
}

function firstParagraph(md) {
  const lines = md.split(/\r?\n/);
  const buf = [];
  let started = false;
  for (const line of lines) {
    if (!started) {
      if (line.startsWith('#')) continue;
      if (!line.trim()) continue;
      started = true;
    }
    if (!line.trim()) break;
    if (line.startsWith('|') || line.startsWith('```') || line.startsWith('>')) break;
    buf.push(line.trim());
    if (buf.join(' ').length > 240) break;
  }
  const text = buf.join(' ').replace(/\s+/g, ' ').trim();
  return text.length > 280 ? `${text.slice(0, 277)}...` : text || null;
}

function extractControllerMeta(filePath) {
  const text = readUtf8(filePath);
  const route = text.match(/\[Route\("([^"]+)"\)\]/)?.[1] ?? null;
  const className =
    text.match(/public\s+(?:sealed\s+|partial\s+)?class\s+(\w+)/)?.[1] ??
    path.basename(filePath, '.cs');
  const http = [...text.matchAll(/\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]/g)].map(
    (m) => ({
      method: m[1].toUpperCase(),
      template: m[2] ?? '',
    })
  );
  const permissions = [
    ...new Set(
      [...text.matchAll(/HasPermission\(([^)]+)\)/g)].map((m) => m[1].trim())
    ),
  ];
  const authorizeRoles = [
    ...new Set(
      [...text.matchAll(/Authorize\(Roles\s*=\s*([^)]+)\)/g)].map((m) => m[1].trim())
    ),
  ];
  return {
    file: rel(filePath),
    class: className,
    route,
    actions: http.slice(0, 40),
    actionCount: http.length,
    permissions,
    authorizeRoles,
  };
}

function extractServiceMeta(filePath) {
  const text = readUtf8(filePath);
  const name = path.basename(filePath, '.cs');
  const isInterface = name.startsWith('I') && /^I[A-Z]/.test(name);
  const methods = [
    ...text.matchAll(
      /(?:Task(?:<[^>]+>)?|ValueTask(?:<[^>]+>)?|void)\s+(\w+)\s*\(/g
    ),
  ]
    .map((m) => m[1])
    .filter((n) => n !== 'Dispose' && n !== 'DisposeAsync');
  return {
    file: rel(filePath),
    name,
    kind: isInterface ? 'interface' : 'class',
    methodCount: methods.length,
    methods: methods.slice(0, 25),
  };
}

function featureInventory(featuresRoot) {
  if (!fs.existsSync(featuresRoot)) return [];
  return fs
    .readdirSync(featuresRoot, { withFileTypes: true })
    .filter((d) => d.isDirectory() && !d.name.startsWith('.'))
    .map((d) => {
      const dir = path.join(featuresRoot, d.name);
      const files = walkFiles(dir, (f) => /\.(tsx?|jsx?)$/.test(f));
      const buckets = { components: 0, hooks: 0, api: 0, utils: 0, other: 0 };
      for (const f of files) {
        const r = rel(f);
        if (r.includes('/components/')) buckets.components++;
        else if (r.includes('/hooks/')) buckets.hooks++;
        else if (r.includes('/api/')) buckets.api++;
        else if (r.includes('/utils/')) buckets.utils++;
        else buckets.other++;
      }
      return {
        name: d.name,
        path: rel(dir),
        fileCount: files.length,
        ...buckets,
        sampleFiles: files.slice(0, 8).map(rel),
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

function indexMarkdownFiles(files) {
  return files
    .map((f) => {
      try {
        const md = readUtf8(f);
        return {
          path: rel(f),
          title: firstHeading(md),
          summary: firstParagraph(md),
          bytes: Buffer.byteLength(md, 'utf8'),
        };
      } catch {
        return { path: rel(f), title: null, summary: null, bytes: 0 };
      }
    })
    .sort((a, b) => a.path.localeCompare(b.path));
}

function configSectionKeys(obj, prefix = '') {
  const keys = [];
  if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return keys;
  for (const [k, v] of Object.entries(obj)) {
    const full = prefix ? `${prefix}:${k}` : k;
    keys.push(full);
    if (v && typeof v === 'object' && !Array.isArray(v)) {
      // one level of nesting for env-style __ sections is enough at top;
      // recurse two levels max for structure without dumping values
      if (prefix.split(':').length < 2) {
        keys.push(...configSectionKeys(v, full));
      }
    }
  }
  return keys;
}

function loadExampleConfigPatterns() {
  const examples = [
    'backend/appsettings.example.json',
    'backend/appsettings.Development.example.json',
    'backend/appsettings.Production.example.json',
  ];
  const out = [];
  for (const p of examples) {
    const full = path.join(ROOT, p);
    if (!fs.existsSync(full)) continue;
    const json = JSON.parse(readUtf8(full));
    const sections = Object.keys(json);
    out.push({
      file: p,
      note: 'Example only — secrets via user-secrets / env; never commit SecretKey or passwords.',
      topLevelSections: sections,
      sectionKeysSample: configSectionKeys(json).slice(0, 80),
    });
  }
  return out;
}

function envVariablePatterns() {
  return {
    backend: [
      {
        name: 'ConnectionStrings__DefaultConnection',
        purpose: 'PostgreSQL connection (user-secrets / env)',
        example: 'Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=***',
      },
      {
        name: 'JwtSettings__SecretKey',
        purpose: 'JWT signing key (≥ 32 chars)',
        example: '***',
      },
      {
        name: 'ASPNETCORE_ENVIRONMENT',
        purpose: 'Development | Production',
        example: 'Development',
      },
      {
        name: 'TwoFactorAuth__Enabled',
        purpose: 'SuperAdmin TOTP challenge in Production',
        example: 'true',
      },
      {
        name: 'TwoFactorAuth__BypassInDevelopment',
        purpose: 'Skip 2FA challenge when Environment=Development',
        example: 'true',
      },
      {
        name: 'Security__Csrf__Enabled',
        purpose: 'CSRF double-submit for mutating verbs',
        example: 'true (Production)',
      },
      {
        name: 'FinanzOnline__Mode',
        purpose: 'Simulation | Production SOAP',
        example: 'Simulation',
      },
    ],
    frontend_admin: [
      {
        name: 'NEXT_PUBLIC_API_BASE_URL',
        purpose: 'API origin for FA',
        example: 'http://localhost:5184',
      },
      {
        name: 'NEXT_PUBLIC_RKSV_ENVIRONMENT',
        purpose: 'RKSV test/prod UI hint',
        example: 'TEST',
      },
    ],
    frontend_pos: [
      {
        name: 'EXPO_PUBLIC_API_BASE_URL',
        purpose: 'API base including /api',
        example: 'http://192.168.1.100:5184/api',
      },
      {
        name: 'EXPO_PUBLIC_DEV_TENANT_ID',
        purpose: 'Dev tenant slug for POS switcher',
        example: 'dev',
      },
    ],
    frontend_sites: [
      {
        name: 'NEXT_PUBLIC_API_BASE_URL',
        purpose: 'API origin for storefront',
        example: 'http://localhost:5184',
      },
    ],
    headers_dev_only: [
      {
        name: 'X-Tenant-Id',
        purpose: 'Tenant slug header (Development only)',
        example: 'dev',
      },
      {
        name: '?tenant=',
        purpose: 'Tenant slug query (Development only)',
        example: '?tenant=dev',
      },
    ],
    refs: ['backend/README.md', 'AGENTS.md', 'docs/MULTI_TENANT.md', 'docs/AUTH_TWO_FACTOR.md'],
  };
}

function main() {
  const curated = JSON.parse(readUtf8(CURATED));

  const serviceFiles = walkFiles(path.join(ROOT, 'backend', 'Services'), (f, name) =>
    name.endsWith('.cs')
  );
  const controllerFiles = walkFiles(
    path.join(ROOT, 'backend', 'Controllers'),
    (f, name) => name.endsWith('.cs')
  );

  const services = serviceFiles.map(extractServiceMeta).sort((a, b) => a.file.localeCompare(b.file));
  const controllers = controllerFiles
    .map(extractControllerMeta)
    .sort((a, b) => a.file.localeCompare(b.file));

  const features = featureInventory(path.join(ROOT, 'frontend-admin', 'src', 'features'));

  const docsFiles = walkFiles(path.join(ROOT, 'docs'), (f, name) => name.endsWith('.md'));
  const readmeFiles = walkFiles(ROOT, (f, name) => name.toLowerCase() === 'readme.md').filter(
    (f) => !rel(f).includes('testsprite_tests/')
  );

  const apiContractFiles = [
    'API_CONTRACT.md',
    'ai/03_API_CONTRACT.md',
    'docs/API_CONTRACTS.md',
    'frontend-admin/docs/api-contract.md',
  ]
    .map((p) => path.join(ROOT, p))
    .filter((p) => fs.existsSync(p));

  const documentation = {
    docs: indexMarkdownFiles(docsFiles),
    readmes: indexMarkdownFiles(readmeFiles),
    apiContracts: indexMarkdownFiles(apiContractFiles),
    agentContracts: indexMarkdownFiles(
      walkFiles(path.join(ROOT, 'ai'), (f, name) => name.endsWith('.md') && !rel(f).startsWith('ai/training/'))
    ),
  };

  const dataset = {
    project: 'Regkasse',
    type: 'monorepo',
    description: 'RKSV-compliant multi-tenant POS system (Austria)',
    generatedAt: new Date().toISOString().slice(0, 10),
    generator: 'scripts/generate-ai-training-dataset.mjs',
    usage:
      'Curated patterns + path inventory for AI agents. Prefer reading source files over inventing APIs. Full rules: AGENTS.md. Secrets are never embedded.',
    stack: {
      backend: 'ASP.NET Core 10 / EF Core 10 / C#',
      frontendAdmin: 'Next.js 16 / Ant Design 6 / TypeScript',
      mobilePos: 'Expo SDK 56 / TypeScript',
      sites: 'Next.js 16 / TypeScript',
    },
    inventory: {
      services: {
        count: services.length,
        interfaces: services.filter((s) => s.kind === 'interface').length,
        items: services,
      },
      controllers: {
        count: controllers.length,
        items: controllers,
      },
      components: {
        note: 'FA feature modules under frontend-admin/src/features/*',
        featureCount: features.length,
        features,
      },
    },
    patterns: curated.patterns,
    examples: curated.examples,
    documentation: [
      ...documentation.docs.map((d) => ({ ...d, kind: 'docs' })),
      ...documentation.readmes.map((d) => ({ ...d, kind: 'readme' })),
      ...documentation.apiContracts.map((d) => ({ ...d, kind: 'api_contract' })),
      ...documentation.agentContracts.map((d) => ({ ...d, kind: 'ai_contract' })),
    ],
    documentationIndex: documentation,
    configuration: {
      appsettingsExamples: loadExampleConfigPatterns(),
      environmentVariables: envVariablePatterns(),
    },
    guardrails: {
      crossTenantHttp: 404,
      apiBoundaries: {
        pos: ['/api/pos/*', '/api/Auth/*', '/api/Receipts/*'],
        admin: ['/api/admin/*', '/api/Auth/*'],
        sites: ['/api/public/*', '/api/sites/*'],
        legacyDoNotExtend: ['/api/Payment', '/api/Cart', '/api/Product'],
      },
      highRiskRefs: ['ai/07_DO_NOT_TOUCH.md', 'AGENTS.md'],
      languages: {
        posUi: 'de-DE',
        adminUi: 'i18n de/en/tr',
        ideExplanations: 'tr',
        codeIdentifiers: 'en',
      },
    },
  };

  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, `${JSON.stringify(dataset, null, 2)}\n`, 'utf8');

  const sizeKb = Math.round(fs.statSync(OUT).size / 1024);
  console.log(
    `Wrote ${rel(OUT)} (${sizeKb} KB) — services=${services.length}, controllers=${controllers.length}, features=${features.length}, docs=${documentation.docs.length}`
  );
}

main();
