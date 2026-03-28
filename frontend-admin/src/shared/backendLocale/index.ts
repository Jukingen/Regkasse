export {
  pickDualLocaleMessage,
  pickDeOnlyBackendText,
  joinDeOnlyBackendList,
  formatRejectionReasonForDisplay,
} from './selectBackendLocalizedText';
export {
  resolveFiscalReportBackendText,
  joinFiscalReportRemediationHints,
  resolveFiscalExportProfileRow,
  resolveLegalExportCompletenessIssueMessage,
  fiscalReportFieldTooltip,
  type FiscalReportResolvedText,
  type FiscalReportContentLang,
  type FiscalExportProfileBackendRow,
  type LegalExportCompletenessIssueText,
  type ReportContentLanguage,
} from './fiscalReportTextPolicy';
/** Prefer `@/shared/reporting/formalReportContentResolver` for new reporting code — same policy, explicit names. */
export {
  FORMAL_REPORT_CONTENT_POLICY_VERSION,
  resolveFormalReportContentFromDualFields,
  joinFormalReportRemediationHints,
  resolveFormalReportExportProfileRow,
  resolveFormalReportLegalExportIssueMessage,
  formalReportBackendFieldTooltip,
} from '../reporting/formalReportContentResolver';
