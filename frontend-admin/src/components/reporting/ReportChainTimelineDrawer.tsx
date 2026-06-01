'use client';

/**
 * Korrektur-Kette / Versions-Timeline für formale Berichte (API: GET /api/reports/history/{type}/{id}).
 */
import React from 'react';
import { Drawer, Spin, Timeline, Typography, Tag, Empty } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useI18n } from '@/i18n/I18nProvider';
import { ReportDocumentBadge, ReportSubmissionBadge } from '@/components/reporting/ReportWorkspaceBadges';

export type FormalReportTypeKey = 'tagesbericht' | 'monatsbericht' | 'jahresbericht';

type TimelineItem = {
  reportId: string;
  reportVersion: number;
  reportStatus: string;
  createdAtUtc: string;
  finalizedAtUtc?: string | null;
  isCurrentActiveVersion?: boolean;
  isCorrectionVersion?: boolean;
  submission: { lifecycle: string };
};

type TimelineDto = {
  reportType: string;
  requestedReportId: string;
  chainRootReportId: string;
  currentActiveReportId?: string | null;
  items: TimelineItem[];
};

export function ReportChainTimelineDrawer(props: {
  open: boolean;
  onClose: () => void;
  reportType: FormalReportTypeKey;
  reportId: string | null;
}) {
  const { open, onClose, reportType, reportId } = props;
  const { t } = useI18n();

  const q = useQuery({
    queryKey: ['report-history', reportType, reportId],
    queryFn: async () => {
      const { data } = await AXIOS_INSTANCE.get<TimelineDto>(`/api/reports/history/${reportType}/${reportId}`);
      return data;
    },
    enabled: open && !!reportId,
  });

  return (
    <Drawer
      title={t('adminShell.reporting.reportCenter.chainDrawerTitle')}
      size={480}
      open={open}
      onClose={onClose}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('adminShell.reporting.reportCenter.chainDrawerIntro')}
      </Typography.Paragraph>
      <Spin spinning={q.isLoading}>
        {!q.isLoading && !q.data?.items?.length ? (
          <Empty description={t('adminShell.reporting.reportCenter.chainEmpty')} />
        ) : !q.isLoading && q.data?.items?.length ? (
          <Timeline
            items={q.data.items.map((it) => ({
              color: it.isCurrentActiveVersion ? 'green' : 'gray',
              children: (
                <div>
                  <div style={{ marginBottom: 6 }}>
                    <Typography.Text strong>
                      {t('adminShell.reporting.reportCenter.chainVersion', { n: String(it.reportVersion) })}
                    </Typography.Text>{' '}
                    {it.isCurrentActiveVersion ? (
                      <Tag color="blue">{t('adminShell.reporting.reportCenter.chainCurrent')}</Tag>
                    ) : null}
                    {it.isCorrectionVersion ? (
                      <Tag>{t('adminShell.reporting.reportCenter.chainCorrection')}</Tag>
                    ) : null}
                  </div>
                  <div style={{ marginBottom: 6 }}>
                    <ReportDocumentBadge status={it.reportStatus} t={t} />{' '}
                    <ReportSubmissionBadge lifecycle={it.submission?.lifecycle} t={t} />
                  </div>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {it.createdAtUtc}
                  </Typography.Text>
                </div>
              ),
            }))}
          />
        ) : null}
      </Spin>
    </Drawer>
  );
}
