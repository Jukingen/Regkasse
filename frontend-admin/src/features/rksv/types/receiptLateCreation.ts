/** Late-creation fields on receipts (until OpenAPI client is regenerated). */
export type ReceiptLateCreationFields = {
    isLateCreated?: boolean;
    lateCreationReason?: string | null;
    intendedPeriodDate?: string | null;
};

export function receiptIsLateCreated(row: ReceiptLateCreationFields): boolean {
    return row.isLateCreated === true;
}
