/** Vienna calendar parts for Fiskaly sign-test Sonderbelege. */
export function viennaNowParts(now: Date = new Date()): { year: number; month: number } {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Vienna',
    year: 'numeric',
    month: 'numeric',
  }).formatToParts(now);
  const year = Number(parts.find((p) => p.type === 'year')?.value ?? now.getUTCFullYear());
  const month = Number(parts.find((p) => p.type === 'month')?.value ?? 1);
  return { year, month };
}

/** Last completed Vienna month — Monatsbeleg cannot target the unfinished current month. */
export function previousViennaMonth(now: Date = new Date()): { year: number; month: number } {
  const current = viennaNowParts(now);
  if (current.month === 1) {
    return { year: current.year - 1, month: 12 };
  }
  return { year: current.year, month: current.month - 1 };
}

/** Previous Vienna calendar year — typical Jahresbeleg target. */
export function previousViennaYear(now: Date = new Date()): number {
  return viennaNowParts(now).year - 1;
}
