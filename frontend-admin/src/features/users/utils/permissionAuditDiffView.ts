/**
 * Summaries and table rows for permission audit diff modal.
 */
import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';
import {
  buildPermissionAuditDiff,
  type PermissionDiffChangeKind,
  type PermissionDiffLine,
  type PermissionStateKind,
} from '@/features/users/utils/permissionAuditDiff';
import type { AuditLogEntryDto } from '@/api/generated/model/auditLogEntryDto';

export type PermissionDiffSummary = {
  changed: number;
  added: number;
  removed: number;
};

export type PermissionDiffTableRow = {
  key: string;
  permissionKey: string;
  oldState: PermissionStateKind | null;
  newState: PermissionStateKind | null;
  change: PermissionDiffChangeKind;
  /** Visual marker: yellow / green / red / white */
  marker: 'yellow' | 'green' | 'red' | 'white';
};

export function summarizePermissionDiffLines(
  lines: readonly PermissionDiffLine[]
): PermissionDiffSummary {
  let added = 0;
  let removed = 0;
  let changed = 0;
  for (const line of lines) {
    if (line.change === 'added') added += 1;
    else if (line.change === 'removed') removed += 1;
    else if (line.change === 'changed' || line.change === 'lifecycle') changed += 1;
  }
  return { changed, added, removed };
}

function markerForChange(change: PermissionDiffChangeKind): PermissionDiffTableRow['marker'] {
  switch (change) {
    case 'added':
      return 'green';
    case 'removed':
      return 'red';
    case 'changed':
      return 'yellow';
    case 'lifecycle':
      return 'yellow';
    default:
      return 'white';
  }
}

/** Only rows that actually changed (exclude unchanged). */
export function toPermissionDiffTableRows(
  lines: readonly PermissionDiffLine[]
): PermissionDiffTableRow[] {
  return lines
    .filter((l) => l.change !== 'lifecycle' || Boolean(l.permissionKey))
    .filter((l) => l.change !== undefined)
    .map((l, idx) => ({
      key: `${l.permissionKey ?? 'row'}-${l.change}-${idx}`,
      permissionKey: l.permissionKey ?? '—',
      oldState: l.oldState,
      newState: l.newState,
      change: l.change,
      marker: markerForChange(l.change),
    }))
    .filter((r) => r.change !== 'lifecycle' || r.oldState !== r.newState);
}

export function linesFromDedicatedEntry(entry: PermissionAuditEntry): PermissionDiffLine[] {
  const oldState = (entry.oldValue as PermissionStateKind | null) ?? null;
  const newState = (entry.newValue as PermissionStateKind | null) ?? null;
  let change: PermissionDiffChangeKind = 'changed';
  if (entry.action === 'created' || entry.action === 'deleted') change = 'lifecycle';
  else if (
    (oldState === 'absent' || oldState === 'denied' || !oldState) &&
    (newState === 'allowed' || newState === 'individual')
  ) {
    change = 'added';
  } else if (
    (oldState === 'allowed' || oldState === 'individual') &&
    (newState === 'absent' || newState === 'denied' || !newState)
  ) {
    change = 'removed';
  }
  return [
    {
      permissionKey: entry.permissionKey || entry.roleName || null,
      change,
      oldState,
      newState,
    },
  ];
}

export function linesFromLegacyAuditEntry(entry: AuditLogEntryDto): PermissionDiffLine[] {
  return buildPermissionAuditDiff(entry).lines.filter((l) => l.change !== undefined);
}

export function formatPermissionDiffClipboard(
  rows: readonly PermissionDiffTableRow[],
  meta: { roleName?: string; timestamp?: string; actor?: string },
  stateLabel: (s: PermissionStateKind | null) => string
): string {
  const header = [
    meta.timestamp ? `Timestamp: ${meta.timestamp}` : null,
    meta.roleName ? `Role: ${meta.roleName}` : null,
    meta.actor ? `Actor: ${meta.actor}` : null,
    '',
    'Permission\tBefore\tAfter\tChange',
  ]
    .filter((x) => x != null)
    .join('\n');
  const body = rows
    .map(
      (r) =>
        `${r.permissionKey}\t${stateLabel(r.oldState)}\t${stateLabel(r.newState)}\t${r.change}`
    )
    .join('\n');
  return `${header}\n${body}`;
}
