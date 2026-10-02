#!/usr/bin/env node
/**
 * Build a commit script from the live working tree.
 * Does not stage, commit, or push.
 *
 *   node scripts/commit-plan-dynamic.mjs
 *
 * Writes the shell script to the OS temp dir (Git Bash /tmp on this machine).
 */
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const scriptPath = path.join(os.tmpdir(), "commit-plan-dynamic.sh");

/** Lowest id wins when a path matches more than one group. */
const groups = [
  {
    id: 1,
    message: "feat(country): add Peppol, e-invoicing, and DE/CH fiscal adapters",
    patterns: [
      /^backend\/Services\/Countries\/.*/,
      /^backend\/Models\/Countries\/.*/,
      /^backend\/Controllers\/AdminChQr.*/,
      /^backend\/Controllers\/AdminPeppol.*/,
      /^backend\/Controllers\/AdminMwst.*/,
      /^frontend-admin\/src\/features\/(peppol|kassenSicherheit|mwst|invoices)\/.*/,
      /^docs\/(COUNTRIES|FISCAL_|EINVOICING_).*\.md$/,
    ],
  },
  {
    id: 2,
    message: "feat(vertical-profiles): add profile catalog, tenant assignment, and POS context",
    patterns: [
      /^backend\/(Models|Services)\/VerticalProfile.*/,
      /^frontend-admin\/src\/features\/vertical-profiles\/.*/,
      /^frontend\/contexts\/VerticalProfileContext\.tsx$/,
    ],
  },
  {
    id: 3,
    message:
      "feat(vertical-profiles): add vet, hair-salon, mobile-services, taxi, handy-shop, and ticket-sales",
    patterns: [
      /^backend\/Controllers\/(AdminLodging|AdminProductImeis|AdminStaff|AdminTicketRedemptions|PosAppointments|PosProductImeis|PosRooms|PosStaff|PosTickets)/,
      /^backend\/Services\/(Appointments|Imei|Lodging|Tickets)\/.*/,
      /^backend\/Models\/(Appointment|GuestFolio|ProductImei|Room|TicketRedemption).*/,
      /^backend\/Migrations\/.*(VetAndHairSalon|AddAppointments|Prescription|ImeiTracking|TicketProduct|TicketRedemptions|MobileService|Beherbergung|RoomsAndGuestFolios).*/,
      /^frontend-admin\/src\/features\/(lodging|tickets)\/.*/,
      /^frontend-admin\/src\/api\/admin\/(lodging|product-imeis|staff|ticket-redemptions)\.ts$/,
      /^frontend\/app\/.*\/(appointment|patient-record|rooms|ticket-validate)\.tsx$/,
      /^frontend\/(components|contexts|services)\/.*(Appointment|Imei|MobileService|RoomPicker|StaffPicker|Taxi|Folio|ticket).*/,
      /^docs\/(BEHERBERGUNG|TICKETS)\.md$/,
    ],
  },
  {
    id: 4,
    message: "fix(ci): enforce migration chronology and CI gates",
    patterns: [
      /^\.github\/workflows\/.*\.yml$/,
      /^backend\/.*MigrationAttribute.*/,
      /^backend\/.*MigrationChronology.*/,
    ],
  },
  {
    id: 5,
    message: "fix(migrations): align snapshot with kitchen and guest-folio indexes",
    patterns: [
      /^backend\/Migrations\/AppDbContextModelSnapshot\.cs$/,
      /^backend\/Migrations\/.*SyncKitchen.*/,
    ],
  },
  {
    id: 6,
    message: "fix(pos): resolve typecheck and lint errors from vertical-profile work",
    patterns: [
      /^frontend\/services\/api\/.*Service\.ts$/,
      // Path contains a real "(tabs)" directory; escape so the group is literal.
      /^frontend\/app\/\(tabs\)\/_.*\.tsx$/,
    ],
  },
  {
    id: 7,
    message: "chore(infra): update DbContext, DI, and cross-cutting entities",
    patterns: [
      /^backend\/Data\/AppDbContext\.cs$/,
      /^backend\/ApplicationHost\.cs$/,
      /^backend\/Services\/PaymentService\.cs$/,
      /^backend\/swagger\.json$/,
      /(^|\/)package(-lock)?\.json$/,
      /^backend\/Startup(MigrationGuard|BootstrapRunner)\.cs$/,
      /^backend\/KasseAPI_Final\.Tests\/PendingMigrationsGuardTests\.cs$/,
    ],
  },
  {
    id: 8,
    message: "docs: update agent rules and repo documentation",
    patterns: [/\.md$/],
  },
  {
    id: 9,
    message: "chore: add commit planning scripts",
    patterns: [/^commit-plan\.sh$/, /^scripts\/commit-plan-dynamic\.mjs$/],
  },
];

function shQuote(value) {
  return `'${value.replace(/'/g, `'\\''`)}'`;
}

function unquoteGitPath(raw) {
  if (!(raw.startsWith('"') && raw.endsWith('"'))) return raw;
  return raw
    .slice(1, -1)
    .replace(/\\([\\"nt])/g, (_, ch) => (ch === "n" ? "\n" : ch === "t" ? "\t" : ch));
}

function parsePorcelain(text) {
  const files = [];
  for (const line of text.split(/\r?\n/)) {
    if (!line) continue;
    let rest = line.slice(3);
    if (rest.includes(" -> ")) rest = rest.split(" -> ").at(-1);
    const filePath = unquoteGitPath(rest).replaceAll("\\", "/");
    if (!filePath || filePath.endsWith("/")) continue;
    files.push(filePath);
  }
  return files;
}

function matchingGroups(filePath) {
  return groups.filter((group) => group.patterns.some((pattern) => pattern.test(filePath)));
}

const porcelain = execFileSync("git", ["status", "--porcelain", "-uall"], {
  cwd: repoRoot,
  encoding: "utf8",
});

const seen = new Set();
const assigned = new Map(groups.map((group) => [group.id, []]));
const ambiguous = [];
const unassigned = [];
const missing = [];

for (const filePath of parsePorcelain(porcelain)) {
  if (seen.has(filePath)) continue;
  seen.add(filePath);

  const absolute = path.join(repoRoot, filePath);
  if (!fs.existsSync(absolute) || !fs.statSync(absolute).isFile()) {
    missing.push(filePath);
    continue;
  }

  const hits = matchingGroups(filePath);
  if (hits.length === 0) {
    unassigned.push(filePath);
    continue;
  }

  const winner = hits.reduce((best, group) => (group.id < best.id ? group : best));
  assigned.get(winner.id).push(filePath);
  if (hits.length > 1) {
    ambiguous.push({
      path: filePath,
      groups: hits.map((group) => group.id).sort((a, b) => a - b),
      assignedTo: winner.id,
    });
  }
}

const usedGroups = groups.filter((group) => assigned.get(group.id).length > 0);
const lines = [
  "#!/usr/bin/env bash",
  "# Generated by scripts/commit-plan-dynamic.mjs",
  "# Does not push. Review before running.",
  "set -euo pipefail",
  "",
  "git restore --staged :/",
  "",
];

for (const group of usedGroups) {
  const files = assigned.get(group.id).slice().sort();
  lines.push(`# Commit ${group.id}`);
  lines.push("git add -- \\");
  files.forEach((filePath, index) => {
    const suffix = index === files.length - 1 ? "" : " \\";
    lines.push(`  ${shQuote(filePath)}${suffix}`);
  });
  lines.push(`git commit -m ${shQuote(group.message)}`);
  lines.push("");
}

lines.push("git status");
lines.push("git log --oneline -12");
lines.push("");
fs.writeFileSync(scriptPath, lines.join("\n"), "utf8");

console.log(`script: ${scriptPath}`);
console.log(`files seen: ${seen.size}`);
for (const group of groups) {
  console.log(`group ${group.id}: ${assigned.get(group.id).length}`);
}
console.log(`unassigned: ${unassigned.length}`);
for (const filePath of unassigned) console.log(`  UNASSIGNED ${filePath}`);
console.log(`ambiguous: ${ambiguous.length}`);
for (const item of ambiguous) {
  console.log(
    `  AMBIGUOUS ${item.path} matches ${item.groups.join(",")} -> commit ${item.assignedTo}`,
  );
}
console.log(`excluded missing: ${missing.length}`);
for (const filePath of missing) console.log(`  MISSING ${filePath}`);
