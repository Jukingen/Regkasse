const HEADER_RE = /^(license|key|lizenz|anahtar)/i;

/** Parse unified REGK keys from CSV / paste (first column per line). */
export function parseLicenseKeysFromText(text: string): string[] {
  const seen = new Set<string>();
  const keys: string[] = [];

  for (const rawLine of text.split(/\r?\n/)) {
    const firstCell = rawLine.split(/[,;\t]/)[0]?.trim().replace(/^["']|["']$/g, '') ?? '';
    if (!firstCell || HEADER_RE.test(firstCell)) continue;
    if (!/^REGK-/i.test(firstCell)) continue;
    const key = firstCell.toUpperCase();
    if (seen.has(key)) continue;
    seen.add(key);
    keys.push(firstCell.trim());
  }

  return keys;
}
