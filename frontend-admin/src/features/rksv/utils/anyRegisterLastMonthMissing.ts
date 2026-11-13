/** True when any overview row reports the previous Vienna month as missing. */
export function anyRegisterLastMonthMissing(
  items:
    | readonly { status?: { lastMonthMissing?: boolean | null } | null }[]
    | null
    | undefined
): boolean {
  if (!items?.length) return false;
  return items.some((row) => row.status?.lastMonthMissing === true);
}
