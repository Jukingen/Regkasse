/**
 * Helpers for stable Ant Design Table `rowKey` values.
 * Prefer real entity ids; when absent, use field composites — never raw array index.
 */

/** Join field parts into a composite key segment (empty → `_`). */
export function compositeRowKeyPart(value: string | number | null | undefined): string {
  if (value == null || value === '') return '_';
  return String(value);
}

/**
 * Builds unique row keys from field parts for each row.
 * Duplicate composites get a `#n` occurrence suffix (not the array index).
 */
export function buildStableRowKeys<T>(
  rows: readonly T[],
  getParts: (row: T) => ReadonlyArray<string | number | null | undefined>
): string[] {
  const seen = new Map<string, number>();
  return rows.map((row) => {
    const base = getParts(row).map(compositeRowKeyPart).join('|');
    const next = (seen.get(base) ?? 0) + 1;
    seen.set(base, next);
    return next === 1 ? base : `${base}#${next}`;
  });
}

/** Prefer `id`; fall back to a labeled composite so empty string never collides. */
export function entityIdRowKey(
  id: string | null | undefined,
  fallbackParts: ReadonlyArray<string | number | null | undefined>
): string {
  const trimmed = typeof id === 'string' ? id.trim() : '';
  if (trimmed) return trimmed;
  return `missing|${fallbackParts.map(compositeRowKeyPart).join('|')}`;
}
