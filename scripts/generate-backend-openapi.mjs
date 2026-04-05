#!/usr/bin/env node
/**
 * Regenerates backend/swagger.json from the real Swashbuckle pipeline (same as runtime AddSwaggerGen + DocInclusionPredicate).
 *
 * Prerequisites: .NET SDK (matches backend target), backend/dotnet-tools.json (Swashbuckle.AspNetCore.Cli).
 *
 * Usage (repository root):
 *   node scripts/generate-backend-openapi.mjs
 *
 * Implementation: KasseAPI_Final.SwaggerHostFactory + OpenApi export mode (see backend/SwaggerHostFactory.cs).
 */
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const backend = join(root, 'backend');

function run(cmd, opts = {}) {
  execSync(cmd, { cwd: backend, stdio: 'inherit', ...opts });
}

run('dotnet build -c Release KasseAPI_Final.csproj');
run('dotnet tool restore');
run(
  'dotnet tool run swagger -- tofile --output swagger.json bin/Release/net10.0/KasseAPI_Final.dll v1'
);

console.log('OK: wrote backend/swagger.json via Swashbuckle CLI (SwaggerHostFactory).');
