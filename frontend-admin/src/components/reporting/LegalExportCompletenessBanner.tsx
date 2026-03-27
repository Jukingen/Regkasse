'use client';

/**
 * Legal Compliance profili veya donmuş Periodenbericht bağlamında backend legal-export-completeness sonucunu gösterir.
 * OperationalPreview / DiagnosticPackage bu kapıdan etkilenmez (ayrı sorgu yok).
 */
import { Alert, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';

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
        <Typography.Text type="secondary">Legal-Export-Prüfung…</Typography.Text>
      </div>
    );
  }
  if (q.isError || !q.data) {
    return (
      <Alert
        type="warning"
        showIcon
        style={{ marginTop: 8 }}
        message="Legal-Export-Prüfung nicht verfügbar."
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
        message="Legal Compliance Export: Snapshot vollständig (keine offenen Blocker)."
      />
    );
  }

  const alertType = gate === 'blocked' ? 'error' : gate === 'allowed_with_warnings' ? 'warning' : 'success';
  const title =
    gate === 'blocked'
      ? 'Legal Compliance Export blockiert'
      : gate === 'allowed_with_warnings'
        ? 'Legal Compliance Export mit Warnungen möglich'
        : 'Legal Compliance Export';

  return (
    <Alert
      type={alertType}
      showIcon
      style={{ marginTop: 8 }}
      message={title}
      description={
        <ul style={{ margin: 0, paddingLeft: 18 }}>
          {issues.map((i) => (
            <li key={`${i.code}-${i.messageDe}`}>
              <Typography.Text type={i.severity === 'block' ? 'danger' : 'warning'}>
                [{i.code}] {i.messageDe}
              </Typography.Text>
            </li>
          ))}
        </ul>
      }
    />
  );
}
