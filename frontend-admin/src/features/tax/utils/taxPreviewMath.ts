/** Austrian MwSt money rounding (2 decimals, away from zero). */
export function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export type TaxAmountBreakdown = {
  net: number;
  ratePercent: number;
  tax: number;
  gross: number;
};

/** Net-based VAT: tax = net * rate/100, gross = net + tax. */
export function calculateTaxFromNet(net: number, ratePercent: number): TaxAmountBreakdown {
  const safeNet = Number.isFinite(net) ? Math.max(0, net) : 0;
  const safeRate = Number.isFinite(ratePercent) ? Math.max(0, ratePercent) : 0;
  const tax = roundMoney(safeNet * (safeRate / 100));
  const gross = roundMoney(safeNet + tax);
  return {
    net: roundMoney(safeNet),
    ratePercent: safeRate,
    tax,
    gross,
  };
}

export function grossDifference(currentGross: number, newGross: number): number {
  return roundMoney(newGross - currentGross);
}
