'use client';

/**
 * RKSV operations landing: read-only investigation tiles (drill-down links). Uses POST analyze only as a
 * snapshot query for the payload-hash card — no repair or payment mutations on this page.
 */

import { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
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
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import {
  getApiAdminFinanzonlineReconciliationMetrics,
  getApiAdminOfflineIntentCoverage,
  postApiAdminOfflinePayloadHashAnalyze,
  getApiAdminOperationsSummary,
} from '@/api/generated/admin/admin';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  buildCoverageCardCopy,
  buildExportRiskCardCopy,
  buildFinanzOnlineCardCopy,
  buildPayloadHashCardCopy,
  buildReplaySummaryCardCopy,
  healthLabelDe,
  healthTagColor,
  mapCoverageSummaryToHealth,
  mapExportRiskToHealth,
  mapFinanzOnlineMetricsToHealth,
  mapPayloadHashAnalyzeToHealth,
  mapReplaySummaryToHealth,
  type OfflineIntentCoverageSummaryInput,
} from '../normalizers';
import { OPERATOR_FO_SUMMARY_SCREEN_COPY, OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { buildRksvMenuGroups } from '@/shared/rksvMenuModel';
import type { OpsHealthLevel } from '../types';
import type { GetApiAdminOfflineIntentCoverageParams, GetApiAdminOperationsSummaryParams } from '@/api/generated/model';

const PAYLOAD_QUICK_MAX_ROWS = 5000;

/** Consistent grid gutter for dashboard rows (horizontal, vertical). */
const ROW_GUTTER: [number, number] = [16, 16];

function OpsHealthCard(props: {
  title: string;
  level: OpsHealthLevel;
  /** First fetch: skeleton placeholder (avoids misleading health tags before data exists). */
  pending: boolean;
  /** Background refetch while previous data is shown (section-local, non-blocking). */
  refetching: boolean;
  summaryLine: string;
  detailLines?: string[];
  ctaHref: string;
  ctaLabel: string;
  footnote?: string;
}) {
  const { token } = theme.useToken();
  const {
    title,
    level,
    pending,
    refetching,
    summaryLine,
    detailLines,
    ctaHref,
    ctaLabel,
    footnote,
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
        <Skeleton active title={false} paragraph={{ rows: 3 }} aria-label="Karteninhalt wird geladen" />
      </Card>
    );
  }

  return (
    <Card
      size="small"
      style={{ height: '100%' }}
      styles={{ body: { minHeight: 168, position: 'relative' } }}
    >
      {refetching ? (
        <Tooltip title="Karte wird aktualisiert …">
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
            <Spin size="small" aria-label="Aktualisiere Karteninhalt" />
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
              <Tooltip title="API-Aufruf fehlgeschlagen oder keine Antwort — Status nicht bewertbar.">
                <Tag color={healthTagColor(level)}>{healthLabelDe(level)}</Tag>
              </Tooltip>
            ) : (
              <Tag color={healthTagColor(level)}>{healthLabelDe(level)}</Tag>
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
          <Link href={ctaHref} passHref legacyBehavior>
            <Button
              type="link"
              size="small"
              icon={<LinkOutlined aria-hidden />}
              style={{ paddingLeft: 0, height: 'auto' }}
            >
              {ctaLabel}
            </Button>
          </Link>
          {footnote && (
            <Typography.Text type="secondary" style={{ fontSize: token.fontSizeSM, lineHeight: token.lineHeightSM }}>
              {footnote}
            </Typography.Text>
          )}
        </Space>
      </div>
    </Card>
  );
}

function DrillTile(props: { title: string; line: string; href: string; action: string }) {
  const { token } = theme.useToken();
  return (
    <Card size="small" style={{ height: '100%' }} role="region" aria-label={props.title}>
      <Typography.Text strong>{props.title}</Typography.Text>
      <Typography.Paragraph
        type="secondary"
        style={{
          fontSize: token.fontSize,
          marginTop: token.marginXS,
          marginBottom: token.marginXS,
          lineHeight: token.lineHeight,
        }}
      >
        {props.line}
      </Typography.Paragraph>
      <Link href={props.href} passHref legacyBehavior>
        <Button type="link" size="small" icon={<LinkOutlined aria-hidden />} style={{ paddingLeft: 0, height: 'auto' }}>
          {props.action}
        </Button>
      </Link>
    </Card>
  );
}

export function RksvOperationsDashboard() {
  const { token } = theme.useToken();
  const queryClient = useQueryClient();
  /** Fixed window for dashboard card only; refetch uses same bounds until remount. */
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

  const coverageSummary: OfflineIntentCoverageSummaryInput | null = coverageQuery.data
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
  const replayLevel = opsSummaryQuery.isError ? ('unavailable' as const) : mapReplaySummaryToHealth(opsSummaryQuery.data);
  const exportRiskLevel = opsSummaryQuery.isError ? ('unavailable' as const) : mapExportRiskToHealth(opsSummaryQuery.data);

  const payloadCopy = buildPayloadHashCardCopy(payloadQuery.data, payloadLevel);
  const coverageCopy = buildCoverageCardCopy(coverageSummary, coverageLevel);
  const foCopy = buildFinanzOnlineCardCopy(foQuery.data, foLevel);
  const replayCopy = buildReplaySummaryCardCopy(opsSummaryQuery.data, replayLevel);
  const exportRiskCopy = buildExportRiskCardCopy(opsSummaryQuery.data, exportRiskLevel);

  const onRefresh = useCallback(async () => {
    setHeaderRefreshBusy(true);
    try {
      await queryClient.refetchQueries({ queryKey: ['rksv-operations'] });
      setRefreshedAt(new Date());
    } finally {
      setHeaderRefreshBusy(false);
    }
  }, [queryClient]);

  return (
    <div style={{ paddingBottom: token.marginXL }}>
      <AdminPageHeader
        title={ADMIN_NAV_LABELS.rksvOperationsOverview}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.rksvOperationsOverview }]}
        actions={
          <Space>
            <Typography.Text type="secondary" style={{ fontSize: token.fontSizeSM, lineHeight: token.lineHeightSM }}>
              Stand: {refreshedAt.toLocaleString('de-AT')}
            </Typography.Text>
            <Button
              type="default"
              icon={<ReloadOutlined aria-hidden />}
              onClick={() => void onRefresh()}
              loading={headerRefreshBusy}
            >
              Daten aktualisieren
            </Button>
          </Space>
        }
      />

      <Typography.Paragraph type="secondary" style={{ marginBottom: token.margin, lineHeight: token.lineHeightLG }}>
        <Typography.Text strong style={{ color: 'inherit' }}>
          Kurzüberblick.
        </Typography.Text>{' '}
        Einstiege und Status — wo API-Daten vorliegen, nur leichte Zusammenfassungen; sonst Links zu den Werkzeugen.
        Details nur auf den Zielseiten.
      </Typography.Paragraph>

      <Card
        size="small"
        title="Nach Arbeitsauftrag"
        style={{ marginBottom: token.marginLG }}
        styles={{ body: { paddingTop: token.paddingSM } }}
      >
        <Typography.Paragraph
          type="secondary"
          style={{ marginTop: 0, marginBottom: token.margin, fontSize: token.fontSize, lineHeight: token.lineHeightLG }}
        >
          <Typography.Text strong style={{ color: 'inherit' }}>
            Nach Aufgabe wählen.
          </Typography.Text>{' '}
          Dieselben Ziele wie in der Seitenleiste. Darunter: Status-Signale und Werkzeug-Kacheln.
        </Typography.Paragraph>
        <Row gutter={ROW_GUTTER}>
          {buildRksvMenuGroups(OPERATOR_VERIFICATIONS_COPY.navMenuLabel).map((group) => (
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
                  {group.groupLabel}
                </Typography.Text>
                <Typography.Paragraph
                  type="secondary"
                  style={{
                    fontSize: token.fontSizeSM,
                    marginBottom: token.marginSM,
                    lineHeight: token.lineHeightSM,
                  }}
                >
                  {group.hubTaskLine}
                </Typography.Paragraph>
                <Space direction="vertical" size={token.marginXXS} style={{ width: '100%' }}>
                  {group.items.map((item) => (
                    <Link key={item.key} href={item.href} passHref legacyBehavior>
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
                        {item.label}
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
        Status
      </Typography.Title>
      <Row gutter={ROW_GUTTER} style={{ marginBottom: token.marginLG }}>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title="Payload-Hash"
            level={payloadLevel}
            pending={payloadQuery.isPending}
            refetching={payloadQuery.isFetching && !payloadQuery.isPending}
            summaryLine={payloadCopy.summaryLine}
            detailLines={payloadCopy.detailLines}
            ctaHref="/rksv/payload-hash-conflicts"
            ctaLabel="Payload-Hash-Konflikte öffnen"
            footnote={`Stichprobe: max. ${PAYLOAD_QUICK_MAX_ROWS} Zeilen — kein vollständiger Bestand.`}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title="Offline-Intent-Abdeckung"
            level={coverageLevel}
            pending={coverageQuery.isPending}
            refetching={coverageQuery.isFetching && !coverageQuery.isPending}
            summaryLine={coverageCopy.summaryLine}
            detailLines={coverageCopy.detailLines}
            ctaHref="/rksv/offline-intent-coverage"
            ctaLabel="Details zur Abdeckung öffnen"
            footnote="Festes Fenster: letzte 24 h UTC (nur diese Karte)."
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title={OPERATOR_FO_SUMMARY_SCREEN_COPY.dashboardMetricsCardTitle}
            level={foLevel}
            pending={foQuery.isPending}
            refetching={foQuery.isFetching && !foQuery.isPending}
            summaryLine={foCopy.summaryLine}
            detailLines={foCopy.detailLines}
            ctaHref="/rksv/finanz-online-queue"
            ctaLabel="FinanzOnline-Abgleich öffnen"
            footnote={OPERATOR_FO_SUMMARY_SCREEN_COPY.dashboardMetricsCardFootnote}
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title="Replay und Incident"
            level={replayLevel}
            pending={opsSummaryQuery.isPending}
            refetching={opsSummaryQuery.isFetching && !opsSummaryQuery.isPending}
            summaryLine={replayCopy.summaryLine}
            detailLines={[
              ...replayCopy.detailLines,
              `Incident-Korrelationen (24 h): ${opsSummaryQuery.data?.incidentCorrelationCount ?? 0}`,
            ]}
            ctaHref="/rksv/incident"
            ctaLabel="Incident öffnen"
            footnote="Übersicht (24 h): Rückstand, endgültige Fehler und Incident-Dichte — verbindendes Signal, keine Einzelfallgarantie."
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <OpsHealthCard
            title="Export-Risiko"
            level={exportRiskLevel}
            pending={opsSummaryQuery.isPending}
            refetching={opsSummaryQuery.isFetching && !opsSummaryQuery.isPending}
            summaryLine={exportRiskCopy.summaryLine}
            detailLines={exportRiskCopy.detailLines}
            ctaHref="/rksv/fiscal-export-diagnostics"
            ctaLabel="Diagnose öffnen"
            footnote="Die Übersicht nutzt Integritätschecks nur als ersten Hinweis zum Export-Risiko."
          />
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginBottom: token.marginSM }}>
        Werkzeuge
      </Typography.Title>
      <Row gutter={ROW_GUTTER} style={{ marginBottom: token.marginLG }}>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Incident-Untersuchung"
            line="Correlation-ID, Audit-Spur und FinanzOnline-Versuche in einem Ablauf."
            href="/rksv/incident"
            action="Incident öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Replay-Batches"
            line="Batch nach Correlation-ID suchen und Details anzeigen."
            href="/rksv/replay-batch"
            action="Suche öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Payload-Hash-Konflikte / Behebung"
            line="Analyse, CSV-Export; Behebung nur mit Systemberechtigung."
            href="/rksv/payload-hash-conflicts"
            action="Konflikte öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Fiscal-Export-Diagnose"
            line="JSON-Vorschau, Integritätshinweise, Download."
            href="/rksv/fiscal-export-diagnostics"
            action="Diagnose öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Datenintegrität (Support)"
            line="DB-weite Checks: Belegsequenz, Refund-Orphans, Zahlung ohne Rechnung."
            href="/rksv/integrity"
            action="Integrität öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Abdeckung und Rollout"
            line="Pro Kasse: Geräte-ID- und Sequenz-Abdeckung, höchstes Risiko."
            href="/rksv/offline-intent-coverage"
            action="Abdeckung öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="Audit / Korrelation"
            line="Globale Audit-Logs mit Filtern; Korrelation oft über Incident verknüpft."
            href="/audit-logs"
            action="Audit-Logs öffnen"
          />
        </Col>
        <Col xs={24} sm={12} md={12} lg={8} xl={6}>
          <DrillTile
            title="FinanzOnline · Fehler und erneutes Senden"
            line="Liste, Filter, erneutes Senden einzelner Übermittlungen."
            href="/rksv/finanz-online-queue"
            action="Abgleich öffnen"
          />
        </Col>
      </Row>

      <Typography.Title level={2} style={{ marginBottom: token.marginSM }}>
        Kurznotizen
      </Typography.Title>
      <Alert
        type="info"
        showIcon
        message="Hinweise zu den Karten"
        role="note"
        description={
          <ul style={{ marginBottom: 0, paddingLeft: token.paddingLG }}>
            <li>
              <strong>OK</strong>: laut dieser API-Antwort keine Auffälligkeit im genannten Umfang — nicht automatisch
              vollständiger Datenbestand.
            </li>
            <li>
              <strong>Hinweis</strong> / <strong>Kritisch</strong>: Ampelfarbe nur zusammen mit Kurztext und Detailseite
              interpretieren.
            </li>
            <li>
              <strong>Nicht verfügbar</strong>: Aufruf fehlgeschlagen — nicht als „alles gut“ lesen.
            </li>
            <li>
              Payload: POST-Analyse, begrenzte Zeilen. Abdeckung: GET-Zusammenfassung, 24 h UTC. FinanzOnline:
              GET-Metriken.
            </li>
            <li>
              Replay/Export: Dashboard nutzt `api/admin/operations/summary` (24 h) als verbindendes Signal.
            </li>
          </ul>
        }
      />
    </div>
  );
}
