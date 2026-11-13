/**
 * Guards documented mirrors between `operatorTruthCopy.ts` and `src/i18n/locales/de/*.json`.
 * See `docs/OPERATOR_COPY_AND_RUNTIME_I18N.md`. Does not import app code that requires env (e.g. axios).
 */
import { describe, expect, it } from 'vitest';

import { OPERATOR_INVOICE_COPY, OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';

import { getLocaleStringAtPath, loadDeLocaleRoot } from './helpers/readDeLocaleJson';

type SharedField = keyof typeof OPERATOR_SHARED_COPY;

/** Must stay in sync with the table in `operatorTruthCopy.ts` and `OPERATOR_COPY_AND_RUNTIME_I18N.md`. */
const OPERATOR_SHARED_TO_DE_COMMON: { field: SharedField; deCommonPath: string }[] = [
  { field: 'unknownErrorDetail', deCommonPath: 'messages.noTechnicalDetail' },
  { field: 'loadFailedList', deCommonPath: 'loadErrors.list' },
  { field: 'loadFailedBatch', deCommonPath: 'loadErrors.batch' },
  { field: 'loadFailedIncident', deCommonPath: 'loadErrors.incidentAggregate' },
  { field: 'notFoundIncidentTitle', deCommonPath: 'incident.aggregateNotFoundTitle' },
  { field: 'notFoundIncidentDescription', deCommonPath: 'incident.aggregateNotFoundDescription' },
  { field: 'loadingIncident', deCommonPath: 'loading.incidentAggregate' },
  { field: 'loadingBatchDetail', deCommonPath: 'loading.batchDetail' },
  { field: 'loadingInvoiceDetail', deCommonPath: 'loading.invoiceDetail' },
  { field: 'emptyBatchForCorrelation', deCommonPath: 'empty.batchDetailsForCorrelation' },
  { field: 'refetchHintToolbar', deCommonPath: 'toolbar.refetchHint' },
  { field: 'investigateFurtherLabel', deCommonPath: 'investigation.furtherLabel' },
  { field: 'retryLoadShort', deCommonPath: 'buttons.reload' },
  { field: 'toolbarRefresh', deCommonPath: 'buttons.refresh' },
  { field: 'retryAfterError', deCommonPath: 'buttons.retry' },
];

/** Documented in `OPERATOR_COPY_AND_RUNTIME_I18N.md` (truth-critical provenance + export contract). */
const OPERATOR_INVOICE_TO_DE_INVOICES: {
  operatorField: keyof typeof OPERATOR_INVOICE_COPY;
  deInvoicesPath: string;
}[] = [
  { operatorField: 'detailProvenanceFooter', deInvoicesPath: 'detail.provenanceOperatorFooter' },
  { operatorField: 'csvExportHeaderRow', deInvoicesPath: 'export.csvHeaderRow' },
];

describe('operator copy ↔ de locale parity (documented mirrors only)', () => {
  it('OPERATOR_SHARED_COPY matches de/common.json paths', () => {
    const commonDe = loadDeLocaleRoot('common');
    for (const { field, deCommonPath } of OPERATOR_SHARED_TO_DE_COMMON) {
      const canonical = OPERATOR_SHARED_COPY[field];
      const runtime = getLocaleStringAtPath(commonDe, deCommonPath);
      expect(
        runtime,
        `Missing or wrong type at de/common.json → ${deCommonPath} (mirror of OPERATOR_SHARED_COPY.${String(field)})`
      ).toBeTypeOf('string');
      expect(
        canonical,
        `OPERATOR_SHARED_COPY.${String(field)} must equal de/common.json "${deCommonPath}"`
      ).toBe(runtime);
    }
  });

  it('OPERATOR_INVOICE_COPY documented pairs match de/invoices.json paths', () => {
    const invoicesDe = loadDeLocaleRoot('invoices');
    for (const { operatorField, deInvoicesPath } of OPERATOR_INVOICE_TO_DE_INVOICES) {
      const canonical = OPERATOR_INVOICE_COPY[operatorField];
      const runtime = getLocaleStringAtPath(invoicesDe, deInvoicesPath);
      expect(
        runtime,
        `Missing or wrong type at de/invoices.json → ${deInvoicesPath} (mirror of OPERATOR_INVOICE_COPY.${String(operatorField)})`
      ).toBeTypeOf('string');
      expect(
        canonical,
        `OPERATOR_INVOICE_COPY.${String(operatorField)} must equal de/invoices.json "${deInvoicesPath}"`
      ).toBe(runtime);
    }
  });
});
