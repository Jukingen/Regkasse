'use client';

/**
 * Legal Compliance profile (or frozen Periodenbericht context): shows backend legal-export-completeness.
 * OperationalPreview / DiagnosticPackage do not use this gate (no query).
 */
import { Alert, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useI18n } from '@/i18n/I18nProvider';
import { useFiscalReportText } from '@/shared/reporting/useFiscalReportText';

export type LegalExportCompletenessResult = {
  gate: 'allowed' | 'allowed_with_warnings' | 'blocked';
  reportType: string;
  reportId: string;
  issues: { code: string; severity: 'block' | 'warn'; messageDe: string; messageEn?: string }[];
};

type ReportKind = 'tagesbericht' | 'monatsbericht' | 'jahresbericht' | 'periodenbericht';

type Props = {
  reportKind: ReportKind;
  reportId: string | undefined;
  /** true: Legal profili veya Perioden-Modal gibi kapı aktif */
  enabled: boolean;
};

export function LegalExportCompletenessBanner({ reportKind, reportId, enabled }: Props) {
  const { t } = useI18n();
  const { resolveLegalExportCompletenessIssue } = useFiscalReportText();
  const q = useQuery({
    queryKey: ['legal-export-completeness', reportKind, reportId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<LegalExportCompletenessResult>(
        `/api/reports/legal-export-completeness/${reportKind}/${reportId}`,
      );
      return data;
    },
    enabled: enabled && !!reportId,
  });

  if (!enabled) return null;
  if (q.isLoading) {
    return (
      <div style={{ marginTop: 8 }}>
        <Spin size="small" />{' '}
        <Typography.Text type="secondary">{t('reporting.legalExportCompleteness.loading')}</Typography.Text>
      </div>
    );
  }
  if (q.isError || !q.data) {
    return (
      <Alert
        type="warning"
        showIcon
        style={{ marginTop: 8 }}
        title={t('reporting.legalExportCompleteness.unavailable')}
      />
    );
  }

  const { gate, issues } = q.data;

  if (gate === 'allowed' && issues.length === 0) {
    return (
      <Alert
        type="success"
        showIcon
        style={{ marginTop: 8 }}
        title={t('reporting.legalExportCompleteness.successComplete')}
      />
    );
  }

  const alertType = gate === 'blocked' ? 'error' : gate === 'allowed_with_warnings' ? 'warning' : 'success';
  const title =
    gate === 'blocked'
      ? t('reporting.legalExportCompleteness.titleBlocked')
      : gate === 'allowed_with_warnings'
        ? t('reporting.legalExportCompleteness.titleAllowedWithWarnings')
        : t('reporting.legalExportCompleteness.titleGeneric');

  return (
    <Alert
      type={alertType}
      showIcon
      style={{ marginTop: 8 }}
      title={title}
      description={
        <ul style={{ margin: 0, paddingLeft: 18 }}>
          {issues.map((i, idx) => (
            <li key={`${i.code}-${idx}`}>
              <Typography.Text type={i.severity === 'block' ? 'danger' : 'warning'}>
                [{i.code}] {resolveLegalExportCompletenessIssue(i)}
              </Typography.Text>
            </li>
          ))}
        </ul>
      }
    />
  );
}
