#!/usr/bin/env node
/**
 * Kill orphaned Regkasse Next.js / Expo / Metro node workers that survive a
 * crashed or half-stopped `npm run dev` (common Windows RAM leak: thousands
 * of `frontend-admin/.next/dev/build/*.js` children with a dead parent).
 *
 * Safe: only matches node.exe whose CommandLine references this repo AND a
 * known Next/Expo/Metro pattern. Does not kill unrelated Node projects.
 *
 * Usage:
 *   node scripts/cleanup-dev-orphans.mjs
 *   node scripts/cleanup-dev-orphans.mjs --dry-run
 *   node scripts/cleanup-dev-orphans.mjs --orphans-only
 *   npm run dev:cleanup
 *   npm run dev:cleanup:orphans
 *
 * `--orphans-only` matches only `.next/dev/build` worker paths so a live
 * `next dev` / Expo session is not killed when starting a sibling app.
 */

import { spawnSync } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..');
const dryRun = process.argv.includes('--dry-run');
const orphansOnly = process.argv.includes('--orphans-only');

function cleanupWindows() {
  const repoEscaped = repoRoot.replace(/'/g, "''");
  const dry = dryRun ? '$true' : '$false';
  const orphans = orphansOnly ? '$true' : '$false';
  const selfPid = process.pid;
  // Match CommandLine substrings. Prefer path fragments that identify Next
  // Turbopack workers and Expo/Metro for this repo only.
  const ps = `
$ErrorActionPreference = 'SilentlyContinue'
$repo = '${repoEscaped}'
$dryRun = ${dry}
$orphansOnly = ${orphans}
$selfPid = ${selfPid}
$repoNorm = $repo.Replace('/', '\\').TrimEnd('\\')
if ($orphansOnly) {
  $fragments = @(
    ($repoNorm + '\\frontend-admin\\.next\\dev\\build'),
    ($repoNorm + '\\frontend-sites\\.next\\dev\\build')
  )
} else {
  $fragments = @(
    ($repoNorm + '\\frontend-admin\\.next'),
    ($repoNorm + '\\frontend-sites\\.next'),
    ($repoNorm + '\\frontend\\.next'),
    ($repoNorm + '\\node_modules\\next\\dist'),
    ($repoNorm + '\\node_modules\\expo\\bin'),
    ($repoNorm + '\\node_modules\\@expo'),
    ($repoNorm + '\\node_modules\\metro'),
    'frontend-admin\\.next\\dev\\build',
    'frontend-sites\\.next\\dev\\build'
  )
}
$matched = @()
Get-CimInstance Win32_Process -Filter "Name='node.exe'" | ForEach-Object {
  if ($_.ProcessId -eq $selfPid) { return }
  $cmd = $_.CommandLine
  if ([string]::IsNullOrWhiteSpace($cmd)) { return }
  if ($cmd.IndexOf($repoNorm, [StringComparison]::OrdinalIgnoreCase) -lt 0) { return }
  if ($cmd -match 'cleanup-dev-orphans\\.mjs|dev-workspaces\\.mjs') { return }
  $hit = $false
  foreach ($f in $fragments) {
    if ($cmd.IndexOf($f, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hit = $true; break }
  }
  if (-not $hit) { return }
  $matched += $_
}
Write-Output ("matched=" + $matched.Count)
foreach ($p in $matched) {
  $short = if ($p.CommandLine.Length -gt 120) { $p.CommandLine.Substring(0,120) + '...' } else { $p.CommandLine }
  Write-Output ("pid=" + $p.ProcessId + " " + $short)
  if (-not $dryRun) {
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
  }
}
`;

  const result = spawnSync(
    'powershell.exe',
    ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', ps],
    { encoding: 'utf8', windowsHide: true },
  );

  if (result.stdout) {
    process.stdout.write(result.stdout);
  }
  if (result.stderr) {
    process.stderr.write(result.stderr);
  }
  if (result.status !== 0 && result.status !== null) {
    console.error(`[cleanup-dev-orphans] PowerShell exited with code ${result.status}`);
    return 1;
  }

  const matchLine = (result.stdout || '')
    .split(/\r?\n/)
    .find((l) => l.startsWith('matched='));
  const count = matchLine ? Number(matchLine.slice('matched='.length)) : 0;
  const modeLabel = orphansOnly ? ' (orphans-only)' : '';
  if (dryRun) {
    console.log(
      `[cleanup-dev-orphans] dry-run${modeLabel}: would kill ${count} Regkasse node worker(s)`,
    );
  } else if (count > 0) {
    console.log(`[cleanup-dev-orphans] killed ${count} Regkasse node worker(s)${modeLabel}`);
  } else {
    console.log(`[cleanup-dev-orphans] no orphan Next/Expo workers found${modeLabel}`);
  }
  return 0;
}

function cleanupUnix() {
  const needles = orphansOnly
    ? [
        path.join(repoRoot, 'frontend-admin', '.next', 'dev', 'build'),
        path.join(repoRoot, 'frontend-sites', '.next', 'dev', 'build'),
      ]
    : [
        path.join(repoRoot, 'frontend-admin', '.next'),
        path.join(repoRoot, 'frontend-sites', '.next'),
        path.join(repoRoot, 'node_modules', 'next', 'dist'),
        path.join(repoRoot, 'node_modules', 'expo', 'bin'),
      ];
  let matched = 0;
  for (const needle of needles) {
    if (dryRun) {
      const list = spawnSync('pgrep', ['-f', needle], { encoding: 'utf8' });
      matched += (list.stdout || '').trim().split(/\s+/).filter(Boolean).length;
      continue;
    }
    spawnSync('pkill', ['-f', needle], { encoding: 'utf8' });
  }
  const modeLabel = orphansOnly ? ' (orphans-only)' : '';
  if (dryRun) {
    console.log(`[cleanup-dev-orphans] dry-run${modeLabel}: matched ~${matched} pid line(s)`);
  } else {
    console.log(`[cleanup-dev-orphans] unix cleanup pass done${modeLabel} (pkill patterns)`);
  }
  return 0;
}

const code = process.platform === 'win32' ? cleanupWindows() : cleanupUnix();
process.exit(code);
