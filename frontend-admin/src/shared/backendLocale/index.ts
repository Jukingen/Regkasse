export {
  type FiscalExportProfileBackendRow,
  type FiscalReportContentLang,
  fiscalReportFieldTooltip,
  type FiscalReportResolvedText,
  joinFiscalReportRemediationHints,
  type LegalExportCompletenessIssueText,
  type ReportContentLanguage,
  resolveFiscalExportProfileRow,
  resolveFiscalReportBackendText,
  resolveLegalExportCompletenessIssueMessage,
} from './fiscalReportTextPolicy';
export {
  formatRejectionReasonForDisplay,
  joinDeOnlyBackendList,
  pickDeOnlyBackendText,
  pickDualLocaleMessage,
} from './selectBackendLocalizedText';
/** Prefer `@/shared/reporting/formalReportContentResolver` for new reporting code — same policy, explicit names. */
export {
  FORMAL_REPORT_CONTENT_POLICY_VERSION,
  formalReportBackendFieldTooltip,
  joinFormalReportRemediationHints,
  resolveFormalReportContentFromDualFields,
  resolveFormalReportExportProfileRow,
  resolveFormalReportLegalExportIssueMessage,
} from '../reporting/formalReportContentResolver';
