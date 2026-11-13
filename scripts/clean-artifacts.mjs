// Cross-platform cleanup of monorepo build artifacts (not node_modules / not Docker volumes).
import { rmSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

const targets = [
  "backend/bin",
  "backend/obj",
  "backend/KasseAPI_Final.Tests/bin",
  "backend/KasseAPI_Final.Tests/obj",
  "frontend/dist",
  "frontend/.expo",
  "frontend-admin/.next",
  "frontend-sites/.next",
  "tools/LicenseGenerator/bin",
  "tools/LicenseGenerator/obj",
  "tools/LicenseGenerator.Core/bin",
  "tools/LicenseGenerator.Core/obj",
  "tools/LicenseGenerator.Web/bin",
  "tools/LicenseGenerator.Web/obj",
];

let removed = 0;
for (const rel of targets) {
  const abs = join(root, rel);
  if (!existsSync(abs)) continue;
  rmSync(abs, { recursive: true, force: true });
  console.log(`removed ${rel}`);
  removed += 1;
}

console.log(removed === 0 ? "clean: nothing to remove" : `clean: removed ${removed} path(s)`);