import type { ReceiptDTO } from '../types/ReceiptDTO';

/** Netto = Brutto − MwSt when stored net is missing or zero on a non-zero receipt. */
export function resolveReceiptNetAmount(input: {
  netTotal?: number | null;
  subtotal?: number | null;
  totalNet?: number | null;
  grandTotal?: number | null;
  taxAmount?: number | null;
}): number {
  const grand = Number(input.grandTotal ?? 0);
  const tax = Number(input.taxAmount ?? 0);
  const derived = grand - tax;
  const candidates = [input.netTotal, input.totalNet, input.subtotal];
  for (const value of candidates) {
    if (value == null || Number.isNaN(Number(value))) continue;
    const n = Number(value);
    if (n !== 0 || grand === 0) return n;
  }
  return derived;
}

function normalizeCompany(raw: Record<string, unknown> | undefined | null): ReceiptDTO['company'] {
  const c = (raw?.company ?? raw?.Company ?? {}) as Record<string, unknown>;
  return {
    name: String(c.name ?? c.Name ?? ''),
    address: String(c.address ?? c.Address ?? ''),
    taxNumber: String(c.taxNumber ?? c.TaxNumber ?? ''),
    description: (c.description ?? c.Description) as string | null | undefined,
  };
}

function normalizeSignature(
  raw: Record<string, unknown> | undefined | null
): ReceiptDTO['signature'] {
  const s = (raw?.signature ?? raw?.Signature) as Record<string, unknown> | undefined | null;
  if (!s) {
    return { algorithm: '', value: '', serialNumber: '', timestamp: '', qrData: '' };
  }
  return {
    algorithm: String(s.algorithm ?? s.Algorithm ?? ''),
    value: String(s.value ?? s.Value ?? s.signatureValue ?? s.SignatureValue ?? ''),
    serialNumber: String(s.serialNumber ?? s.SerialNumber ?? ''),
    timestamp: String(s.timestamp ?? s.Timestamp ?? ''),
    qrData: String(s.qrData ?? s.QrData ?? ''),
  };
}

/** Backend ReceiptDTO (PascalCase or camelCase) → POS ReceiptDTO. */
export function normalizeReceiptDto(raw: unknown): ReceiptDTO {
  const r = (raw ?? {}) as Record<string, unknown>;

  const items = ((r.items ?? r.Items ?? []) as Record<string, unknown>[]).map((i) => ({
    itemId: (i.itemId ?? i.ItemId) as string | undefined,
    name: String(i.name ?? i.Name ?? ''),
    quantity: Number(i.quantity ?? i.Quantity ?? 0),
    unitPrice: Number(i.unitPrice ?? i.UnitPrice ?? 0),
    totalPrice: Number(i.totalPrice ?? i.TotalPrice ?? 0),
    lineTotalNet: (i.lineTotalNet ?? i.LineTotalNet) as number | undefined,
    lineTotalGross: (i.lineTotalGross ?? i.LineTotalGross) as number | undefined,
    taxRate: Number(i.taxRate ?? i.TaxRate ?? 0),
    vatRate: (i.vatRate ?? i.VatRate) as number | undefined,
    vatAmount: (i.vatAmount ?? i.VatAmount) as number | undefined,
    categoryName: (i.categoryName ?? i.CategoryName) as string | null | undefined,
    parentItemId: (i.parentItemId ?? i.ParentItemId) as string | null | undefined,
    isModifierLine: Boolean(i.isModifierLine ?? i.IsModifierLine ?? false),
  }));

  const taxRates = ((r.taxRates ?? r.TaxRates ?? []) as Record<string, unknown>[]).map((t) => ({
    taxType: (t.taxType ?? t.TaxType) as number | undefined,
    rate: Number(t.rate ?? t.Rate ?? 0),
    vatRate: (t.vatRate ?? t.VatRate) as number | undefined,
    netAmount: Number(t.netAmount ?? t.NetAmount ?? 0),
    taxAmount: Number(t.taxAmount ?? t.TaxAmount ?? 0),
    grossAmount: Number(t.grossAmount ?? t.GrossAmount ?? 0),
  }));

  const payments = ((r.payments ?? r.Payments ?? []) as Record<string, unknown>[]).map((p) => ({
    method: (p.method ?? p.Method ?? 'cash') as ReceiptDTO['payments'][number]['method'],
    amount: Number(p.amount ?? p.Amount ?? 0),
    tendered: (p.tendered ?? p.Tendered) as number | undefined,
    change: (p.change ?? p.Change) as number | undefined,
  }));

  const rawTotals = (r.totals ?? r.Totals) as Record<string, unknown> | undefined;
  const grandTotal = Number(r.grandTotal ?? r.GrandTotal ?? 0);
  const taxAmount = Number(r.taxAmount ?? r.TaxAmount ?? 0);
  const netTotal = resolveReceiptNetAmount({
    netTotal: (r.netTotal ?? r.NetTotal) as number | undefined,
    totalNet: (rawTotals?.totalNet ?? rawTotals?.TotalNet) as number | undefined,
    // ASP.NET camelCase serializes SubTotal as "subTotal" (not "subtotal").
    subtotal: (r.subtotal ?? r.subTotal ?? r.SubTotal) as number | undefined,
    grandTotal,
    taxAmount,
  });

  return {
    receiptId: String(r.receiptId ?? r.ReceiptId ?? ''),
    receiptNumber: String(r.receiptNumber ?? r.ReceiptNumber ?? ''),
    date: String(r.date ?? r.Date ?? new Date().toISOString()),
    cashierId: String(r.cashierId ?? r.CashierId ?? ''),
    cashierDisplayName: (r.cashierDisplayName ?? r.CashierDisplayName) as string | undefined,
    tableNumber: (r.tableNumber ?? r.TableNumber) as number | undefined,
    company: normalizeCompany(r),
    kassenID: String(
      r.kassenID ?? r.KassenID ?? r.displayRegisterNumber ?? r.DisplayRegisterNumber ?? ''
    ),
    branchName: (r.branchName ?? r.BranchName) as string | null | undefined,
    terminalNumber: (r.terminalNumber ?? r.TerminalNumber) as string | null | undefined,
    items,
    taxRates,
    subtotal: netTotal,
    netTotal,
    taxAmount,
    grandTotal,
    totals: {
      totalNet: netTotal,
      totalVat: Number(rawTotals?.totalVat ?? rawTotals?.TotalVat ?? taxAmount),
      totalGross: Number(rawTotals?.totalGross ?? rawTotals?.TotalGross ?? grandTotal),
    },
    payments,
    footerText: (r.footerText ?? r.FooterText) as string | undefined,
    thankYouMessage: (r.thankYouMessage ?? r.ThankYouMessage) as string | undefined,
    rksvFooterLabel: (r.rksvFooterLabel ?? r.RksvFooterLabel) as string | undefined,
    showDemoLabel:
      typeof (r.showDemoLabel ?? r.ShowDemoLabel) === 'boolean'
        ? Boolean(r.showDemoLabel ?? r.ShowDemoLabel)
        : undefined,
    signature: normalizeSignature(r),
    verificationUrl: (r.verificationUrl ?? r.VerificationUrl) as string | undefined,
    fiscalTraceKind: (r.fiscalTraceKind ?? r.FiscalTraceKind ?? null) as string | null,
    originalPaymentId: (r.originalPaymentId ?? r.OriginalPaymentId ?? null) as string | null,
    originalSaleReceiptId: (r.originalSaleReceiptId ?? r.OriginalSaleReceiptId ?? null) as
      string | null,
  };
}
