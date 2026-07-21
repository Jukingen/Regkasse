/**
 * Thin large time-series for Recharts without losing endpoints.
 * Keeps first/last points and evenly samples the middle.
 */

export function limitChartPoints<T>(data: readonly T[], maxPoints = 96): T[] {
  if (data.length <= maxPoints) {
    return data as T[];
  }
  if (maxPoints < 2) {
    return data.length > 0 ? [data[data.length - 1]!] : [];
  }

  const lastIndex = data.length - 1;
  const out: T[] = [];
  const innerSlots = maxPoints - 2;
  const step = lastIndex / (innerSlots + 1);

  out.push(data[0]!);
  for (let i = 1; i <= innerSlots; i += 1) {
    const index = Math.min(lastIndex - 1, Math.round(i * step));
    const point = data[index]!;
    if (out[out.length - 1] !== point) {
      out.push(point);
    }
  }
  const last = data[lastIndex]!;
  if (out[out.length - 1] !== last) {
    out.push(last);
  }
  return out;
}
