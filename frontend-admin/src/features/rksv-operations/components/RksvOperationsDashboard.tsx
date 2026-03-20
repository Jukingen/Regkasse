'use client';

/**
 * RKSV operations landing: parallel lightweight summaries, isolated per-card errors, drilldown links.
 */

import { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Space,
  Spin,
  Tag,
  Typography,
  theme,
} from 'antd';
import { ReloadOutlined, LinkOutlined } from '@ant-design/icons';
import Link from 'next/link';
import dayjs from 'dayjs';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { analyzeOfflinePayloadHash } from '@/api/offline-payload-hash';
import { getReconciliationMetrics } from '@/api/finanzonline-reconciliation';
import { customInstance } from '@/lib/axios';
import {
  buildCoverageCardCopy,
  buildFinanzOnlineCardCopy,
  buildPayloadHashCardCopy,
  healthLabelDe,
  healthTagColor,
  mapCoverageSummaryToHealth,
  mapFinanzOnlineMetricsToHealth,
  mapPayloadHashAnalyzeToHealth,
  type OfflineIntentCoverageSummaryInput,
} from '../normalizers';
import type { OpsHealthLevel } from '../types';

const PAYLOAD_QUICK_MAX_ROWS = 5000;

type OfflineIntentCoverageResponse = {
  fromUtc: string;
  toUtc: string;
  total: number;
  withDeviceId: number;
  withSequence: number;
  deviceIdCoveragePercent: number;
  sequenceCoveragePercent: number;
  lowCoverageAlert: boolean;
  alertReason?: string | null;
};

const COVERAGE_BASE = '/api/admin/offline-intent-coverage';

const QK_PAYLOAD = ['rksv-operations', 'payload-analyze', PAYLOAD_QUICK_MAX_ROWS] as const;
const QK_FO = ['rksv-operations', 'fo-metrics'] as const;

function OpsHealthCard(props: {
  title: string;
  level: OpsHealthLevel;
  loading: boolean;
  summaryLine: string;
  detailLines?: string[];
  ctaHref: string;
  ctaLabel: string;
  footnote?: string;
}) {
  const { token } = theme.useToken();
  const { title, level, loading, summaryLine, detailLines, ctaHref, ctaLabel, footnote } = props;

  return (
    <Card size="small" style={{ height: '100%' }} styles={{ body: { minHeight: 168 } }}>
      <Space direction="vertical" size="small" style={{ width: '100%' }}>
        <Space align="center" style={{ justifyContent: 'space-between', width: '100%' }}>
          <Typography.Text strong>{title}</Typography.Text>
          <Tag color={healthTagColor(level)}>{healthLabelDe(level)}</Tag>
        </Space>
        {loading ? (
          <Spin size="small" />
        ) : (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
            {summaryLine}
          </Typography.Paragraph>
        )}
        {!loading && detailLines && detailLines.length > 0 && (
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
        <Link href={ctaHref}>
          <Button type="link" size="small" icon={<LinkOutlined />} style={{ paddingLeft: 0 }}>
            {ctaLabel}
          </Button>
        </Link>
        {footnote && (
          <Typography.Text type="secondary" style={{ fontSize: 11 }}>
            {footnote}
          </Typography.Text>
        )}
      </Space>
    </Card>
  );
}

function CtaOnlyCard(props: {
  title: string;
  description: string;
  ctaHref: string;
  ctaLabel: string;
  secondaryHref?: string;
  secondaryLabel?: string;
}) {
  const { title, description, ctaHref, ctaLabel, secondaryHref, secondaryLabel } = props;
  return (
    <Card size="small" style={{ height: '100%' }}>
      <Space direction="vertical" size="small" style={{ width: '100%' }}>
        <Typography.Text strong>{title}</Typography.Text>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 13 }}>
          {description}
        </Typography.Paragraph>
        <Space wrap>
          <Link href={ctaHref}>
            <Button type="primary" size="small">
              {ctaLabel}
            </Button>
          </Link>
          {secondaryHref && secondaryLabel && (
            <Link href={secondaryHref}>
              <Button size="small">{secondaryLabel}</Button>
            </Link>
          )}
        </Space>
      </Space>
    </Card>
  );
}

function DrillTile(props: { title: string; line: string; href: string; action: string }) {
  return (
    <Card size="small" style={{ height: '100%' }}>
      <Typography.Text strong>{props.title}</Typography.Text>
      <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginTop: 8, marginBottom: 8 }}>
        {props.line}
      </Typography.Paragraph>
      <Link href={props.href}>
        <Button type="link" size="small" icon={<LinkOutlined />} style={{ paddingLeft: 0 }}>
          {props.action}
        </Button>
      </Link>
    </Card>
  );
}

export function RksvOperationsDashboard() {
  const queryClient = useQueryClient();
  /** Fixed window for dashboard card only; refetch uses same bounds until remount. */
  const coverageParams = useMemo(
    () => ({
      fromUtc: dayjs().subtract(1, 'day').toISOString(),
      toUtc: dayjs().toISOString(),
    }),
    []
  );
  const coverageQueryKey = useMemo(
    () => ['rksv-operations', 'coverage', coverageParams] as const,
    [coverageParams]
  );

  const [refreshedAt, setRefreshedAt] = useState(() => new Date());

  const payloadQuery = useQuery({
    queryKey: QK_PAYLOAD,
    queryFn: () => analyzeOfflinePayloadHash({ maxRows: PAYLOAD_QUICK_MAX_ROWS }),
    staleTime: 60_000,
  });

  const coverageQuery = useQuery({
    queryKey: coverageQueryKey,
    queryFn: () =>
      customInstance<OfflineIntentCoverageResponse>({
        url: COVERAGE_BASE,
        method: 'GET',
        params: coverageParams,
      }),
    staleTime: 30_000,
  });

  const foQuery = useQuery({
    queryKey: QK_FO,
    queryFn: getReconciliationMetrics,
    staleTime: 15_000,
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

  const payloadCopy = buildPayloadHashCardCopy(payloadQuery.data, payloadLevel);
  const coverageCopy = buildCoverageCardCopy(coverageSummary, coverageLevel);
  const foCopy = buildFinanzOnlineCardCopy(foQuery.data, foLevel);

  const onRefresh = useCallback(async () => {
    await queryClient.refetchQueries({ queryKey: ['rksv-operations'] });
    setRefreshedAt(new Date());
  }, [queryClient]);

  const refreshing =
    payloadQuery.isFetching || coverageQuery.isFetching || foQuery.isFetching;

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title="RKSV Operations"
        breadcrumbs={[
          { title: 'Dashboard', href: '/dashboard' },
          { title: 'RKSV Operations' },
        ]}
        actions={
          <Space>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Stand: {refreshedAt.toLocaleString('de-AT')}
            </Typography.Text>
            <Button icon={<ReloadOutlined />} onClick={() => void onRefresh()} loading={refreshing}>
              Aktualisieren
            </Button>
          </Space>
        }
      />

      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        Kurzüberblick und Einstiege — wo API-Daten da sind, nur leichte Zusammenfassungen; sonst Links zu den
        Werkzeugen. Tiefer geht es nur auf den Zielseiten.
      </Typography.Paragraph>

      <Typography.Title level={5}>Status</Typography.Title>
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} lg={8}>
          <OpsHealthCard
            title="Payload-Hash"
            level={payloadLevel}
            loading={payloadQuery.isLoading}
            summaryLine={payloadCopy.summaryLine}
            detailLines={payloadCopy.detailLines}
            ctaHref="/rksv/payload-hash-conflicts"
            ctaLabel="Payload-Hash Konflikte"
            footnote={`Stichprobe: max. ${PAYLOAD_QUICK_MAX_ROWS} Zeilen — kein vollständiger Bestand.`}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <OpsHealthCard
            title="Offline Intent Coverage"
            level={coverageLevel}
            loading={coverageQuery.isLoading}
            summaryLine={coverageCopy.summaryLine}
            detailLines={coverageCopy.detailLines}
            ctaHref="/rksv/offline-intent-coverage"
            ctaLabel="Coverage-Details"
            footnote="Festes Fenster: letzte 24h UTC (nur diese Karte)."
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <OpsHealthCard
            title="FinanzOnline (Metriken)"
            level={foLevel}
            loading={foQuery.isLoading}
            summaryLine={foCopy.summaryLine}
            detailLines={foCopy.detailLines}
            ctaHref="/rksv/finanz-online-queue"
            ctaLabel="FinanzOnline Abgleich"
            footnote="Zähler aus Metriken-API — bei „OK“ trotzdem Liste prüfen wenn Zweifel."
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <CtaOnlyCard
            title="Replay / Incident"
            description="Kein Dashboard-Signal: es gibt keine globale Batch-Zusammenfassung. Correlation-ID auf der Zielseite eingeben."
            ctaHref="/rksv/incident"
            ctaLabel="Incident (Correlation-ID)"
            secondaryHref="/rksv/replay-batch"
            secondaryLabel="Replay-Batch"
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <CtaOnlyCard
            title="Fiscal-Export Diagnose"
            description="Kein Dashboard-Signal: Vorschau braucht Kasse und Zeitraum — auf der Diagnoseseite."
            ctaHref="/rksv/fiscal-export-diagnostics"
            ctaLabel="Diagnose öffnen"
          />
        </Col>
      </Row>

      <Typography.Title level={5}>Werkzeuge</Typography.Title>
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Incident-Untersuchung"
            line="Correlation-ID, Audit-Trace, FO-Versuche in einem Flow."
            href="/rksv/incident"
            action="Incident öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Replay-Batches"
            line="Batch nach Correlation-ID suchen und Details anzeigen."
            href="/rksv/replay-batch"
            action="Suche öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Payload-Hash Konflikte / Repair"
            line="Analyse, CSV-Export; Repair nur mit System-Berechtigung."
            href="/rksv/payload-hash-conflicts"
            action="Konflikte öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Fiscal-Export Diagnose"
            line="JSON-Vorschau, Integritätshinweise, Download."
            href="/rksv/fiscal-export-diagnostics"
            action="Diagnose öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Datenintegrität (Support)"
            line="DB-weite Checks: Belegsequenz, Refund-Orphans, Zahlung ohne Rechnung."
            href="/rksv/integrity"
            action="Integrität öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Coverage / Rollout"
            line="Pro Kasse: DeviceId- und Sequenz-Abdeckung, Top-Risk."
            href="/rksv/offline-intent-coverage"
            action="Coverage öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="Audit / Korrelation"
            line="Globale Audit-Logs mit Filtern; Korrelation oft über Incident verknüpft."
            href="/audit-logs"
            action="Audit-Logs öffnen"
          />
        </Col>
        <Col xs={24} md={12} lg={8}>
          <DrillTile
            title="FinanzOnline Fehler / Retry"
            line="Liste, Filter, Retry einzelner Submits."
            href="/rksv/finanz-online-queue"
            action="Abgleich öffnen"
          />
        </Col>
      </Row>

      <Typography.Title level={5}>Kurznotizen</Typography.Title>
      <Alert
        type="info"
        showIcon
        message="Hinweise zu den Karten"
        description={
          <ul style={{ marginBottom: 0, paddingLeft: 18 }}>
            <li>
              <strong>OK</strong> = laut dieser API-Antwort keine Auffälligkeit im genannten Umfang — nicht automatisch
              vollständiger Datenbestand.
            </li>
            <li>
              <strong>Nicht verfügbar</strong> = Aufruf fehlgeschlagen — nicht als „alles gut“ lesen.
            </li>
            <li>Payload: POST analyze, begrenzte Zeilen. Coverage: GET summary, 24h UTC. FO: GET metrics.</li>
            <li>Replay / Fiscal-Export: nur Links, keine Summary-API auf dem Dashboard.</li>
          </ul>
        }
      />
    </div>
  );
}
