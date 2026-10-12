export function todayIsoDate(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** Noon UTC on the check-in calendar day, plus optional nights. */
export function stayInstantUtc(isoDate: string, extraNights = 0): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day + extraNights, 12, 0, 0, 0)).toISOString();
}
