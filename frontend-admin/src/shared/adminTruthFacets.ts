/**
 * Contract-first admin truth facets (frontend-admin).
 *
 * - One helper = one semantic dimension. Screens **compose** facets into badges/copy; there is no merged
 *   “row truth status” enum (that would hide distinct concepts).
 * - Register deep-link badge here applies only when the **same DTO’s** `cashRegisterId` is the link source
 *   (invoice list/detail, FO reconciliation table). Incident’s FO-joined register cell still layers
 *   `derived_from_foreign_row` separately — do not replace that composition with this helper alone.
 *
 * **Canonical vs runtime copy:** Invoice provenance footer text is defined for operators in
 * `OPERATOR_INVOICE_COPY.detailProvenanceFooter` (`operatorTruthCopy.ts`). The translatable UI string is
 * `invoices.detail.provenanceOperatorFooter` — keep German entries identical. See
 * `docs/OPERATOR_COPY_AND_RUNTIME_I18N.md`.
 */

import type { Invoice } from '@/api/generated/model/invoice';
import {
    formatInvoiceDataProvenanceForDisplay,
    readOptionalInvoiceDataProvenance,
} from '@/shared/contract/invoiceDetailResponseExtensions';
import type { OperatorTruthBadgeKind } from '@/shared/operatorTruthCopy';
import type { RegisterFkFieldAnalysis } from '@/shared/utils/registerIdentity';

/** Badge pair for “can register-scoped FO deep-link use this row’s API FK?” */
export type RegisterDeepLinkEligibleBadgeKind = Extract<
    OperatorTruthBadgeKind,
    'authoritative_api' | 'link_incomplete'
>;

/**
 * When the authoritative field is `cashRegisterId` on the **current** DTO row (not a joined aggregate).
 */
export function registerDeepLinkEligibleBadgeKind(
    analysis: Pick<RegisterFkFieldAnalysis, 'linkSafeUuid'>,
): RegisterDeepLinkEligibleBadgeKind {
    return analysis.linkSafeUuid ? 'authoritative_api' : 'link_incomplete';
}

/**
 * Invoice row origin: explicit backend discriminator when JSON includes it; otherwise honest OpenAPI gap.
 * No inference from `sourcePaymentId` or other fields.
 */
export type InvoiceProvenanceUiFacet =
    | {
          kind: 'explicit_backend_string';
          raw: string;
          operatorLabel: string;
          typingNote: 'invoiceDataProvenance_not_on_orval_invoice_until_openapi';
      }
    | {
          kind: 'contract_incomplete_no_response_field';
          /** UI: `t('invoices.detail.provenanceOperatorFooter')` (aligned with `OPERATOR_INVOICE_COPY.detailProvenanceFooter`) */
          operatorCopyKey: 'detailProvenanceFooter';
      };

export function invoiceProvenanceUiFacet(invoice: Invoice): InvoiceProvenanceUiFacet {
    const raw = readOptionalInvoiceDataProvenance(invoice);
    if (raw) {
        return {
            kind: 'explicit_backend_string',
            raw,
            operatorLabel: formatInvoiceDataProvenanceForDisplay(raw),
            typingNote: 'invoiceDataProvenance_not_on_orval_invoice_until_openapi',
        };
    }
    return { kind: 'contract_incomplete_no_response_field', operatorCopyKey: 'detailProvenanceFooter' };
}
