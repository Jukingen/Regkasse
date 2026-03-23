import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { parseArgs } from './_shared.mjs';

const args = parseArgs(process.argv);

const baselinePath = args.baseline
  ? path.resolve(args.baseline)
  : path.resolve('localization/baseline/boundary-baseline.json');
if (!args.report) {
  throw new Error('Usage: node localization/scripts/compare-boundary-baseline.mjs --report <path> [--baseline <path>] [--warn-only true]');
}
const reportPaths = [path.resolve(args.report)];
const warnOnly = args['warn-only'] === 'true';
const includeWarnings = args['include-warnings'] !== 'false';
const outPath = args.out
  ? path.resolve(args.out)
  : path.resolve('localization/out/reports/boundary-compare-report.json');
const writeBaseline = args['write-baseline'] === 'true';

const baselineRaw = await fs.readFile(baselinePath, 'utf8');
const baselineJson = JSON.parse(baselineRaw);
const baselineEntries = Array.isArray(baselineJson.entries) ? baselineJson.entries : [];
const baselineSet = new Set(
  baselineEntries.map((entry) =>
    entry.fingerprint ||
    fingerprintFromViolation(entry, process.cwd()),
  ),
);

const currentViolations = [];
for (const reportPath of reportPaths) {
  const raw = await fs.readFile(reportPath, 'utf8');
  const report = JSON.parse(raw);
  const violations = Array.isArray(report.violations) ? report.violations : [];
  for (const violation of violations) {
    if (!includeWarnings && violation.severity === 'warning') continue;
    currentViolations.push({ ...violation, reportPath });
  }
}

const indexedCurrent = [];
for (const violation of currentViolations) {
  const fingerprint = fingerprintFromViolation(violation, process.cwd());
  indexedCurrent.push({ ...violation, fingerprint });
}

const newViolations = indexedCurrent.filter((v) => !baselineSet.has(v.fingerprint));
const bySeverity = {
  error: newViolations.filter((v) => v.severity === 'error').length,
  warning: newViolations.filter((v) => v.severity === 'warning').length,
};

await fs.mkdir(path.dirname(outPath), { recursive: true });
const compareReportPath = outPath;
const compareReport = {
  baselinePath,
  reportPaths,
  includeWarnings,
  warnOnly,
  baselineCount: baselineSet.size,
  currentCount: indexedCurrent.length,
  newCount: newViolations.length,
  newBySeverity: bySeverity,
  newViolations: newViolations
    .sort((a, b) =>
      String(a.app).localeCompare(String(b.app)) ||
      String(a.code).localeCompare(String(b.code)) ||
      String(a.file).localeCompare(String(b.file)) ||
      Number(a.line ?? 0) - Number(b.line ?? 0),
    )
    .map((v) => ({
      fingerprint: v.fingerprint,
      app: v.app ?? null,
      code: v.code ?? null,
      severity: v.severity ?? null,
      file: toRelative(v.file, process.cwd()),
      line: v.line ?? null,
      column: v.column ?? null,
      message: v.message ?? null,
      snippet: v.snippet ?? null,
      reportPath: toRelative(v.reportPath, process.cwd()),
    })),
  timestamp: new Date().toISOString(),
};
await fs.writeFile(compareReportPath, `${JSON.stringify(compareReport, null, 2)}\n`, 'utf8');

console.log(`Boundary compare report: ${compareReportPath}`);
console.log(`Baseline entries: ${baselineSet.size}`);
console.log(`Current violations: ${indexedCurrent.length}`);
console.log(`New violations: ${newViolations.length} (error=${bySeverity.error}, warning=${bySeverity.warning})`);

if (writeBaseline) {
  const snapshot = {
    version: 1,
    generatedAt: new Date().toISOString(),
    sourceReports: reportPaths,
    includeWarnings,
    entries: indexedCurrent
      .sort((a, b) =>
        String(a.app).localeCompare(String(b.app)) ||
        String(a.code).localeCompare(String(b.code)) ||
        String(a.file).localeCompare(String(b.file)) ||
        Number(a.line ?? 0) - Number(b.line ?? 0),
      )
      .map((v) => ({
        fingerprint: v.fingerprint,
        app: v.app ?? null,
        code: v.code ?? null,
        severity: v.severity ?? null,
        file: toRelative(v.file, process.cwd()),
        line: v.line ?? null,
        column: v.column ?? null,
      })),
  };
  await fs.mkdir(path.dirname(baselinePath), { recursive: true });
  await fs.writeFile(baselinePath, `${JSON.stringify(snapshot, null, 2)}\n`, 'utf8');
  console.log(`Baseline snapshot updated: ${baselinePath}`);
}

if (newViolations.length > 0 && !warnOnly) {
  console.error('Boundary baseline compare failed: new violations detected.');
  process.exit(1);
}

if (newViolations.length > 0 && warnOnly) {
  console.warn('Boundary baseline compare warning: new violations detected but warn-only mode is enabled.');
}

function toRelative(filePath, cwd) {
  if (!filePath) return '';
  const abs = path.resolve(String(filePath));
  const rel = path.relative(cwd, abs);
  return rel || '.';
}

function fingerprintFromViolation(violation, cwd) {
  const app = String(violation.app ?? '');
  const code = String(violation.code ?? '');
  const severity = String(violation.severity ?? '');
  const file = toRelative(violation.file ?? '', cwd).replaceAll('\\', '/');
  const line = Number.isFinite(Number(violation.line)) ? Number(violation.line) : 0;
  const column = Number.isFinite(Number(violation.column)) ? Number(violation.column) : 0;
  const snippetHash = crypto
    .createHash('sha1')
    .update(String(violation.snippet ?? '').trim())
    .digest('hex')
    .slice(0, 12);
  return `${app}|${code}|${severity}|${file}|${line}|${column}|${snippetHash}`;
}
