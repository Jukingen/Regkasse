export function roundTaxiFare(value: number): number {
  return Math.round(value * 100) / 100;
}

/** Suggested taxi fare: manual amount wins, otherwise km × tariff. Fiscal totals stay on cart lines. */
export function computeTaxiSuggestedAmount(
  km: number | null,
  tariffPerKm: number | null,
  manualAmount: number | null
): number | null {
  if (manualAmount != null && Number.isFinite(manualAmount) && manualAmount > 0) {
    return roundTaxiFare(manualAmount);
  }
  if (
    km != null &&
    tariffPerKm != null &&
    Number.isFinite(km) &&
    Number.isFinite(tariffPerKm) &&
    km > 0 &&
    tariffPerKm > 0
  ) {
    return roundTaxiFare(km * tariffPerKm);
  }
  return null;
}
