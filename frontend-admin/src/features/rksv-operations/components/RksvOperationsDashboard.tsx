'use client';

/**
 * RKSV / FinanzOnline operational hub: health cards, workflow links, and task groups.
 * Read-only investigation tiles; POST payload analyze is snapshot-only (no repair mutations here).
 */

import { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Collapse,
  List,
  Row,
  Skeleton,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
  theme,
} from 'antd';
import { ReloadOutlined, LinkOutlined } from '@ant-design/icons';
import Link from 'next/link';
import dayjs from 'dayjs';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import {
  getApiAdminFinanzonlineReconciliationMetrics,
  getApiAdminOfflineIntentCoverage,
  postApiAdminOfflinePayloadHashAnalyze,
  getApiAdminOperationsSummary,
} from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  healthTagColor,
  mapCoverageSummaryToHealth,
  mapExportRiskToHealth,
  mapFinanzOnlineMetricsToHealth,
  mapPayloadHashAnalyzeToHealth,
  mapReplaySummaryToHealth,
} from '../normalizers';
import {
  getCoverageHealthCopy,
  getExportRiskHealthCopy,
  getFinanzOnlineHealthCopy,
  getPayloadHealthCopy,
  getReplayHealthCopy,
  type RksvHubTranslate,
} from '../rksvHubHealthCopy';
import { RKSV_HUB_GROUPS } from '../rksvHubNavigation';
import { readRksvPublicEnvironment } from '../rksvHubEnv';
import type { OpsHealthLevel } from '../types';
import type { GetApiAdminOfflineIntentCoverageParams, GetApiAdminOperationsSummaryParams } from '@/api/generated/model';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';

const PAYLOAD_QUICK_MAX_ROWS = 5000;

const ROW_GUTTER: [number, number] = [16, 16];

function labelForHealthLevel(level: OpsHealthLevel, t: RksvHubTranslate): string {
  switch (level) {
    case 'healthy':
      return t('rksvHub.healthLevel.ok');
    case 'warning':
      return t('rksvHub.healthLevel.warning');
    case 'critical':
      return t('rksvHub.healthLevel.critical');
    default:
      return t('rksvHub.healthLevel.unavailable');
  }
}

function OpsHealthCard(props: {
  title: string;
  level: OpsHealthLevel;
  healthLabel: string;
  pending: boolean;
  refetching: boolean;
  summaryLine: string;
  detailLines?: string[];
  primaryHref: string;
  primaryLabel: string;
  secondaryHref?: string;
  secondaryLabel?: string;
  footnote?: string;
  pendingAriaLabel: string;
  refetchAriaLabel: string;
  unavailableTooltip: string;
}) {
  const { token } = theme.useToken();
  const {
    title,
    level,
    healthLabel,
    pending,
    refetching,
    summaryLine,
    detailLines,
    primaryHref,
    primaryLabel,
    secondaryHref,
    secondaryLabel,
    footnote,
    pendingAriaLabel,
    refetchAriaLabel,
    unavailableTooltip,
  } = props;

  if (pending) {
    return (
      <Card
        size="small"
        style={{ height: '100%' }}
        styles={{ body: { minHeight: 168 } }}
        aria-busy="true"
      >
        <Typography.Text strong style={{ display: 'block', marginBottom: token.marginSM }}>
          {title}
        </Typography.Text>
        <Skeleton active title={false} paragraph={{ rows: 3 }} aria-label={pendingAriaLabel} />
      </Card>
    );
  }

  return (
    <Card size="small" style={{ height: '100%' }} styles={{ body: { minHeight: 168, position: 'relative' } }}>
      {refetching ? (
        <Tooltip title={refetchAriaLabel}>
          <span
            style={{
              position: 'absolute',
              top: token.marginSM,
              right: token.marginSM,
              zIndex: 2,
            }}
            role="status"
            aria-live="polite"
          >
            <Spin size="small" aria-label={refetchAriaLabel} />
          </span>
        </Tooltip>
      ) : null}
      <div
        style={{
          opacity: refetching ? 0.72 : 1,
          transition: `opacity ${token.motionDurationMid}`,
        }}
      >
        <Space direction="vertical" size="small" style={{ width: '100%' }}>
          <Space align="center" style={{ justifyContent: 'space-between', width: '100%' }} wrap>
            <Typography.Text strong>{title}</Typography.Text>
            {level === 'unavailable' ? (
              <Tooltip title={unavailableTooltip}>
                <Tag color={healthTagColor(level)}>{healthLabel}</Tag>
              </Tooltip>
            ) : (
              <Tag color={healthTagColor(level)}>{healthLabel}</Tag>
            )}
          </Space>
          <Typography.Paragraph
            type="secondary"
            style={{ marginBottom: 0, fontSize: token.fontSize, lineHeight: token.lineHeight }}
          >
            {summaryLine}
          </Typography.Paragraph>
          {detailLines && detailLines.length > 0 && (
            <Typography.Paragraph
              type="secondary"
              style={{
                marginBottom: 0,
                fontSize: 12,
                fontFamily: 'monospace',
                color: token.colorTextSecondary,
              }}
            >
              {detailLines.slice(0, 4).join(' · ')}
            </Typography.Paragraph>
          )}
          <Space direction="vertical" size={0}>
            <Link href={primaryHref} passHref legacyBehavior>
              <Button
                type="link"
                size="small"
                icon={<LinkOutlined aria-hidden />}
                style={{ paddingLeft: 0, height: 'auto' }}
              >
                {primaryLabel}
              </Button>
            </Link>
            {secondaryHref && secondaryLabel ? (
              <Link href={secondaryHref} passHref legacyBehavior>
                <Button type="link" size="small" style={{ paddingLeft: 0, height: 'auto' }}>
                  {secondaryLabel}
                </Button>
              </Link>
            ) : null}
          </Space>
          {footnote ? (
            <Typography.Text type="secondary" style={{ fontSize: token.fontSizeSM, lineHeight: token.lineHeightSM }}>
              {footnote}
            </Typography.Text>
          ) : null}
        </Space>
      </div>
    </Card>
  );
}

export function RksvOperationsDashboard() {
  const { token } = theme.useToken();
  const { t, formatLocale } = useI18n();
  const hubT: RksvHubTranslate = (key, options) => t(key, options);
  const queryClient = useQueryClient();
  const publicEnv = useMemo(() => readRksvPublicEnvironment(), []);

  const coverageParams = useMemo<GetApiAdminOfflineIntentCoverageParams>(
    () => ({
      fromUtc: dayjs().subtract(1, 'day').toISOString(),
      toUtc: dayjs().toISOString(),
    }),
    []
  );
  const coverageQueryKey = useMemo(
    () => rksvAdminQueryKeys.operations.coverageSummary(coverageParams),
    [coverageParams]
  );

  const [refreshedAt, setRefreshedAt] = useState(() => new Date());
  const [headerRefreshBusy, setHeaderRefreshBusy] = useState(false);
  const operationsSummaryParams = useMemo<GetApiAdminOperationsSummaryParams>(() => ({ windowHours: 24 }), []);

  const payloadQuery = useQuery({
    queryKey: rksvAdminQueryKeys.operations.payloadAnalyzeQuick(PAYLOAD_QUICK_MAX_ROWS),
    queryFn: () => postApiAdminOfflinePayloadHashAnalyze({ maxRows: PAYLOAD_QUICK_MAX_ROWS }),
    staleTime: 60_000,
    placeholderData: keepPreviousData,
  });

  const coverageQuery = useQuery({
    queryKey: coverageQueryKey,
    queryFn: () => getApiAdminOfflineIntentCoverage(coverageParams),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  const foQuery = useQuery({
    queryKey: rksvAdminQueryKeys.operations.foMetrics,
    queryFn: getApiAdminFinanzonlineReconciliationMetrics,
    staleTime: 15_000,
    placeholderData: keepPreviousData,
  });

  const opsSummaryQuery = useQuery({
    queryKey: rksvAdminQueryKeys.operations.summary(operationsSummaryParams),
    queryFn: () => getApiAdminOperationsSummary(operationsSummaryParams),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  const coverageSummary = coverageQuery.data
    ? {
        lowCoverageAlert: coverageQuery.data.lowCoverageAlert,
        alertReason: coverageQuery.data.alertReason,
        deviceIdCoveragePercent: coverageQuery.data.deviceIdCoveragePercent,
        sequenceCoveragePercent: coverageQuery.data.sequenceCoveragePercent,
        total: coverageQuery.data.total,
      }
    : null;

  const payloadLevel = payloadQuery.isError
    ? ('unavailable' as const)
    : mapPayloadHashAnalyzeToHealth(payloadQuery.data);
  const coverageLevel = coverageQuery.isError
    ? ('unavailable' as const)
    : mapCoverageSummaryToHealth(coverageSummary);
  const foLevel = foQuery.isError ? ('unavailable' as const) : mapFinanzOnlineMetricsToHealth(foQuery.data);
  const replayLevel = opsSummaryQuery.isError
    ? ('unavailable' as const)
    : mapReplaySummaryToHealth(opsSummaryQuery.data);
  const exportRiskLevel = opsSummaryQuery.isError
    ? ('unavailable' as const)
    : mapExportRiskToHealth(opsSummaryQuery.data);

  const levels = [payloadLevel, coverageLevel, foLevel, replayLevel, exportRiskLevel];
  const hasCritical = levels.some((l) => l === 'critical');
  const hasWarning = !hasCritical && levels.some((l) => l === 'warning');

  const payloadCopy = getPayloadHealthCopy(payloadQuery.data, payloadLevel, hubT);
  const coverageCopy = getCoverageHealthCopy(coverageSummary, coverageLevel, hubT);
  const foCopy = getFinanzOnlineHealthCopy(foQuery.data, foLevel, hubT);
  const replayCopy = getReplayHealthCopy(opsSummaryQuery.data, replayLevel, hubT);
  const exportRiskCopy = getExportRiskHealthCopy(opsSummaryQuery.data, exportRiskLevel, hubT);

  const onRefresh = useCallback(async () => {
    setHeaderRefreshBusy(true);
    try {
      await queryClient.refetchQueries({ queryKey: ['rksv-operations'] });
      setRefreshedAt(new Date());
    } finally {
      setHeaderRefreshBusy(false);
    }
  }, [queryClient]);

  const refreshedLabel = useMemo(
    () => formatDateTime(refreshedAt, formatLocale),
    [refreshedAt, formatLocale],
  );

  const envBanner =
    publicEnv === 'PROD'
      ? { type: 'error' as const, message: t('rksvHub.env.bannerProd'), description: t('rksvHub.env.hintProd') }
      : publicEnv === 'TEST'
        ? { type: 'info' as const, message: t('rksvHub.env.bannerTest'), description: t('rksvHub.env.hintTest') }
        : { type: 'warning' as const, message: t('rksvHub.env.bannerUnknown'), description: t('rksvHub.env.hintUnknown') };

  const moreToolLinks = useMemo(
    () => [
      { href: '/rksv/payload-hash-conflicts', title: t('rksvHub.moreTools.payloadHash') },
      { href: '/rksv/finanz-online-queue', title: t('rksvHub.moreTools.queueLegacy') },
      { href: '/rksv/fiscal-export-diagnostics', title: t('rksvHub.moreTools.fiscalExport') },
      { href: '/rksv/integrity', title: t('rksvHub.moreTools.integrity') },
      { href: '/rksv/offline-intent-coverage', title: t('rksvHub.moreTools.offlineCoverage') },
      { href: '/rksv/verifications', title: t('rksvHub.moreTools.verifications') },
      { href: '/audit-logs', title: t('rksvHub.moreTools.auditLogs') },
    ],
    [t]
  );

  const pendingAria = t('rksvHub.aria.cardRefreshing');
  const refetchAria = t('rksvHub.aria.cardUpdating');
  const unavailableTip = t('rksvHub.healthLevel.unavailableTooltip');

  return (
    <div style={{ paddingBottom: token.marginXL }}>
      <AdminPageHeader
        title={t('nav.rksvOperationsOverview')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.rksvOperationsOverview') }]}
        actions={
          <Space>
            <Typography.Text type="secondary" style={{ fontSize: token.fontSizeSM, lineHeight: token.lineHeightSM }}>
              {t('rksvHub.refresh.refreshedAt', { time: refreshedLabel })}
            </Typography.Text>
            <Button
              type="default"
              icon={<ReloadOutlined aria-hidden />}
              onClick={() => void onRefresh()}
              loading={headerRefreshBusy}
            >
              {t('rksvHub.refresh.label')}
            </Button>
          </Space>
        }
      />

      <Alert
        showIcon
        type={envBanner.type}
        message={envBanner.message}
        description={envBanner.description}
        style={{ marginBottom: token.marginMD }}
      />

      {hasCritical ? (
        <Alert
          type="error"
          showIcon
          message={t('rksvHub.nextSteps.title')}
          description={t('rksvHub.nextSteps.critical')}
          style={{ marginBottom: token.marginMD }}
        />
      ) : hasWarning ? (
        <Alert
          type="warning"
          showIcon
          message={t('rksvHub.nextSteps.title')}
          description={t('rksvHub.nextSteps.warning')}
          style={{ marginBottom: token.marginMD }}
        />
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: token.margin, lineHeight: token.lineHeightLG }}>
        <Typography.Text strong style={{ color: 'inherit' }}>
          {t('rksvHub.intro.lead')}
        </Typography.Text>{' '}
        {t('rksvHub.intro.body')}
      </Typography.Paragraph>

      <Card
        size="small"
        title={t('rksvHub.sections.workByTask')}
        style={{ marginBottom: token.marginLG }}
        styles={{ body: { paddingTop: token.paddingSM } }}
      >
        <Typography.Paragraph
          type="secondary"
          style={{ marginTop: 0, marginBottom: token.margin, fontSize: token.fontSize, lineHeight: token.lineHeightLG }}
        >
          <Typography.Text strong style={{ color: 'inherit' }}>
            {t('rksvHub.sections.workByTaskIntro')}
          </Typography.Text>{' '}
          {t('rksvHub.sections.workByTaskHint')}
        </Typography.Paragraph>
        <Row gutter={ROW_GUTTER}>
          {RKSV_HUB_GROUPS.map((group) => (
            <Col xs={24} sm={12} md={12} lg={6} key={group.id}>
              <div
                style={{
                  height: '100%',
                  padding: token.paddingSM,
                  borderRadius: token.borderRadiusLG,
                  background: token.colorFillTertiary,
                  border: `1px solid ${token.colorBorderSecondary}`,
                }}
              >
                <Typography.Text strong style={{ display: 'block', marginBottom: token.marginXXS }}>
                  {t(group.titleKey)}
                </Typography.Text>
                <Typography.Paragraph
                  type="secondary"
                  style={{
                    fontSize: token.fontSizeSM,
                    marginBottom: token.marginSM,
                    lineHeight: token.lineHeightSM,
                  }}
                >
                  {t(group.descriptionKey)}
                </Typography.Paragraph>
                <Space direction="vertical" size={token.marginXXS} style={{ width: '100%' }}>
                  {group.links.map((item) => (
                    <Link key={item.href} href={item.href} passHref legacyBehavior>
                      <Button
                        type="link"
                        size="small"
                        icon={<LinkOutlined aria-hidden />}
                        style={{
                          paddingLeft: 0,
                          height: 'auto',
                          justifyContent: 'flex-start',
                          display: 'block',
                          textAlign: 'left',
                        }}
                      >
                        {t(item.labelKey)}
                      </Button>
                    </Link>
                  ))}
                </Space>
              </div>
            </Col>
          ))}
        </Row>
      </Card>

      <Typography.Title level={2} style={{ marginBottom: token.marginSM }}>
        {t('rksvHub.sections.workflow')}
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: token.margin }}>
        {t('rksvHub.sections.workflowHint')}
      </Typography.Paragraph>
      <Row gutter={ROW_GUTTER} style={{ marginBottom: token.marginLG }}>
        <Col xs={24} md={8}>
          <Card size="small" style={{ height: '100%' }}>
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {t('rksvHub.workflow.step1Title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary">{t('rksvHub.workflow.step1Hint')}</Typography.Paragraph>
            <Link href="/rksv/incident" passHref legacyBehavior>
              <Button type="primary" block icon={<LinkOutlined aria-hidden />}>
                {t('rksvHub.workflow.step1Cta')}
              </Button>
            </Link>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small" style={{ height: '100%' }}>
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {t('rksvHub.workflow.step2Title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary">{t('rksvHub.workflow.step2Hint')}</Typography.Paragraph>
            <Link href="/rksv/replay-batch" passHref legacyBehavior>
              <Button type="primary" block icon={<LinkOutlined aria-hidden />}>
                {t('rksvHub.workflow.step2Cta')}
              </Button>
            </Link>
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small" style={{ height: '100%' }}>
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {t('rksvHub.workflow.step3Title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary">{t('rksvHub.workflow.step3Hint')}</Typography.Paragraph>
            <Link href="/rksv/finanz-online-outbox" passHref legacyBehavior>
              <Button type="primary" block icon={<LinkOutlined aria-hidden />}>
                {t('rksvHub.workflow.step3Cta')}
              </Button>
            </Link>
          </Card>
        </Col>
      </Row>

      <Typography.Title level={2} style={{ marginBottom: token.marginSM }}>
        {t('rksvHub.sections.status')}
      </Typography.Title>
      <Row gutter={ROW_GUTTER} style={{ marginBottom: token.marginLG }}>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={t('rksvHub.health.titles.payload')}
            level={payloadLevel}
            healthLabel={labelForHealthLevel(payloadLevel, hubT)}
            pending={payloadQuery.isPending}
            refetching={payloadQuery.isFetching && !payloadQuery.isPending}
            summaryLine={payloadCopy.summaryLine}
            detailLines={payloadCopy.detailLines}
            primaryHref="/rksv/payload-hash-conflicts"
            primaryLabel={t('rksvHub.health.cta.payload')}
            footnote={t('rksvHub.health.cardFootnote.payloadSample', { maxRows: PAYLOAD_QUICK_MAX_ROWS })}
            pendingAriaLabel={pendingAria}
            refetchAriaLabel={refetchAria}
            unavailableTooltip={unavailableTip}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={t('rksvHub.health.titles.coverage')}
            level={coverageLevel}
            healthLabel={labelForHealthLevel(coverageLevel, hubT)}
            pending={coverageQuery.isPending}
            refetching={coverageQuery.isFetching && !coverageQuery.isPending}
            summaryLine={coverageCopy.summaryLine}
            detailLines={coverageCopy.detailLines}
            primaryHref="/rksv/offline-intent-coverage"
            primaryLabel={t('rksvHub.health.cta.coverage')}
            footnote={t('rksvHub.health.cardFootnote.coverageWindow')}
            pendingAriaLabel={pendingAria}
            refetchAriaLabel={refetchAria}
            unavailableTooltip={unavailableTip}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={t('rksvHub.health.titles.finanzOnline')}
            level={foLevel}
            healthLabel={labelForHealthLevel(foLevel, hubT)}
            pending={foQuery.isPending}
            refetching={foQuery.isFetching && !foQuery.isPending}
            summaryLine={foCopy.summaryLine}
            detailLines={foCopy.detailLines}
            primaryHref="/rksv/finanz-online-outbox"
            primaryLabel={t('rksvHub.health.cta.finanzOnlinePrimary')}
            secondaryHref="/rksv/finanz-online-queue"
            secondaryLabel={t('rksvHub.health.cta.finanzOnlineSecondary')}
            footnote={t('rksvHub.health.cardFootnote.finanzOnlineMetrics')}
            pendingAriaLabel={pendingAria}
            refetchAriaLabel={refetchAria}
            unavailableTooltip={unavailableTip}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={t('rksvHub.health.titles.replay')}
            level={replayLevel}
            healthLabel={labelForHealthLevel(replayLevel, hubT)}
            pending={opsSummaryQuery.isPending}
            refetching={opsSummaryQuery.isFetching && !opsSummaryQuery.isPending}
            summaryLine={replayCopy.summaryLine}
            detailLines={replayCopy.detailLines}
            primaryHref="/rksv/incident"
            primaryLabel={t('rksvHub.health.cta.replay')}
            secondaryHref="/rksv/replay-batch"
            secondaryLabel={t('rksvHub.health.cta.replaySecondary')}
            footnote={t('rksvHub.health.cardFootnote.replayWindow')}
            pendingAriaLabel={pendingAria}
            refetchAriaLabel={refetchAria}
            unavailableTooltip={unavailableTip}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={t('rksvHub.health.titles.export')}
            level={exportRiskLevel}
            healthLabel={labelForHealthLevel(exportRiskLevel, hubT)}
            pending={opsSummaryQuery.isPending}
            refetching={opsSummaryQuery.isFetching && !opsSummaryQuery.isPending}
            summaryLine={exportRiskCopy.summaryLine}
            detailLines={exportRiskCopy.detailLines}
            primaryHref="/rksv/fiscal-export-diagnostics"
            primaryLabel={t('rksvHub.health.cta.export')}
            secondaryHref="/rksv/integrity"
            secondaryLabel={t('rksvHub.moreTools.integrity')}
            footnote={t('rksvHub.health.cardFootnote.exportRisk')}
            pendingAriaLabel={pendingAria}
            refetchAriaLabel={refetchAria}
            unavailableTooltip={unavailableTip}
          />
        </Col>
      </Row>

      <Collapse
        style={{ marginBottom: token.marginLG }}
        items={[
          {
            key: 'more',
            label: t('rksvHub.sections.moreTools'),
            children: (
              <List
                size="small"
                dataSource={moreToolLinks}
                renderItem={(item) => (
                  <List.Item>
                    <Link href={item.href}>{item.title}</Link>
                  </List.Item>
                )}
              />
            ),
          },
        ]}
      />

      <Typography.Title level={2} style={{ marginBottom: token.marginSM }}>
        {t('rksvHub.sections.notes')}
      </Typography.Title>
      <Alert
        type="info"
        showIcon
        message={t('rksvHub.notes.title')}
        role="note"
        description={
          <ul style={{ marginBottom: 0, paddingLeft: token.paddingLG }}>
            <li>{t('rksvHub.notes.bullet1')}</li>
            <li>{t('rksvHub.notes.bullet2')}</li>
            <li>{t('rksvHub.notes.bullet3')}</li>
            <li>{t('rksvHub.notes.bullet4')}</li>
            <li>{t('rksvHub.notes.bullet5')}</li>
          </ul>
        }
      />
    </div>
  );
}
