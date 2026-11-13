/**
 * Central entry point for **formal fiscal / accounting report content** language (German or English only).
 * Import from here (or use `useFiscalReportText` in React) for API-provided report prose, export-profile
 * copy, remediation hints, and legal-export completeness messages.
 *
 * This is intentionally separate from:
 * - **General admin UI** — `TextLocale` via `useI18n` (`de` | `en` | `tr`)
 * - **Technical / system logs** — English only (raw backend strings, dev tooling)
 *
 * Turkish (`tr`) is never used as the language of formal report **body** text; resolution always yields
 * `contentLang` in `{ 'de', 'en' }`. See `reportContentLanguagePolicy` for the policy version and rules.
 *
 * @module formalReportContentResolver
 */
export {
  type FiscalExportProfileBackendRow,
  type FiscalReportResolvedText,
  fiscalReportFieldTooltip as formalReportBackendFieldTooltip,
  joinFiscalReportRemediationHints as joinFormalReportRemediationHints,
  type LegalExportCompletenessIssueText,
  resolveFiscalReportBackendText as resolveFormalReportContentFromDualFields,
  resolveFiscalExportProfileRow as resolveFormalReportExportProfileRow,
  resolveLegalExportCompletenessIssueMessage as resolveFormalReportLegalExportIssueMessage,
} from '../backendLocale/fiscalReportTextPolicy';
export type { ReportContentLanguage } from './reportContentLanguagePolicy';
export {
  FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES,
  FORMAL_REPORT_CONTENT_POLICY_VERSION,
  isReportContentLanguage,
  preferredReportContentLanguage,
} from './reportContentLanguagePolicy';
