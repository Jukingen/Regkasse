import type { ReceiptDTO } from '../types/ReceiptDTO';

function normalizeCompany(raw: Record<string, unknown> | undefined | null): ReceiptDTO['company'] {
  const c = (raw?.company ?? raw?.Company ?? {}) as Record<string, unknown>;
  return {
    name: String(c.name ?? c.Name ?? ''),
    address: String(c.address ?? c.Address ?? ''),
    taxNumber: String(c.taxNumber ?? c.TaxNumber ?? ''),
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
    items,
    taxRates,
    subtotal: Number(r.subtotal ?? r.SubTotal ?? 0),
    taxAmount: Number(r.taxAmount ?? r.TaxAmount ?? 0),
    grandTotal: Number(r.grandTotal ?? r.GrandTotal ?? 0),
    totals: (r.totals ?? r.Totals) as ReceiptDTO['totals'],
    payments,
    footerText: (r.footerText ?? r.FooterText) as string | undefined,
    rksvFooterLabel: (r.rksvFooterLabel ?? r.RksvFooterLabel) as string | undefined,
    signature: normalizeSignature(r),
    verificationUrl: (r.verificationUrl ?? r.VerificationUrl) as string | undefined,
    fiscalTraceKind: (r.fiscalTraceKind ?? r.FiscalTraceKind ?? null) as string | null,
    originalPaymentId: (r.originalPaymentId ?? r.OriginalPaymentId ?? null) as string | null,
    originalSaleReceiptId: (r.originalSaleReceiptId ?? r.OriginalSaleReceiptId ?? null) as
      string | null,
  };
}
