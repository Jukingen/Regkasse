'use client';

/**
 * Payload-Hash conflict triage — read-only. Analyze shows ConflictGroups and RepairableItems;
 * CSV export; no repair on this page.
 */

import React, { useMemo, useState } from 'react';
import {
  Card,
  Table,
  Tag,
  Statistic,
  Row,
  Col,
  Spin,
  Alert,
  Button,
  Space,
  Select,
  InputNumber,
  message,
  Typography,
} from 'antd';
import { ReloadOutlined, DownloadOutlined } from '@ant-design/icons';
import { useQuery, useMutation } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  analyzeOfflinePayloadHash,
  downloadExportCsv,
  type PayloadHashConflictGroup,
  type PayloadHashRepairableItem,
  type OfflinePayloadHashAnalyzeResult,
} from '@/api/offline-payload-hash';
import { customInstance } from '@/lib/axios';

interface CashRegisterListResponse {
  registers?: { id: string; registerNumber?: string }[];
}

const QUERY_KEY = ['admin', 'offline-payload-hash', 'analyze'];

function severityColor(severity: string): string {
  if (severity === 'High') return 'red';
  if (severity === 'Medium') return 'orange';
  return 'default';
}

export default function PayloadHashConflictsPage() {
  const [maxRows, setMaxRows] = useState(10_000);
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const analyzeParams = useMemo(
    () => ({ maxRows, cashRegisterId: cashRegisterId ?? undefined }),
    [maxRows, cashRegisterId]
  );

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: [...QUERY_KEY, analyzeParams],
    queryFn: () => analyzeOfflinePayloadHash(analyzeParams),
    staleTime: 60_000,
  });

  const { data: cashRegisters } = useQuery({
    queryKey: ['cash-registers'],
    queryFn: async () =>
      customInstance<CashRegisterListResponse>({ url: '/api/CashRegister', method: 'GET' }),
    staleTime: 60_000,
  });

  const downloadCsvMutation = useMutation({
    mutationFn: () => downloadExportCsv(analyzeParams),
    onSuccess: async (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'offline-payload-hash-analyze.csv';
      a.click();
      URL.revokeObjectURL(url);
      message.success('CSV heruntergeladen');
    },
    onError: (e: Error) => message.error(e?.message ?? 'Export fehlgeschlagen'),
  });

  const conflictColumns = [
    {
      title: 'Kasse',
      dataIndex: 'cashRegisterId',
      key: 'cashRegisterId',
      width: 120,
      render: (v: string) => <Typography.Text code copyable>{v?.slice(0, 8)}…</Typography.Text>,
    },
    {
      title: 'Canonical Hash',
      dataIndex: 'canonicalHash',
      key: 'canonicalHash',
      width: 120,
      ellipsis: true,
      render: (v: string) => <Typography.Text code copyable>{v?.slice(0, 12)}…</Typography.Text>,
    },
    {
      title: 'Grund (Skip)',
      dataIndex: 'skipReason',
      key: 'skipReason',
      width: 180,
      render: (v: string) => <Tag>{v ?? '—'}</Tag>,
    },
    {
      title: 'Priorität',
      dataIndex: 'severitySuggestion',
      key: 'severitySuggestion',
      width: 90,
      render: (v: string) => <Tag color={severityColor(v)}>{v ?? '—'}</Tag>,
    },
    {
      title: 'Neueste (UTC)',
      dataIndex: 'latestCreatedAtUtc',
      key: 'latestCreatedAtUtc',
      width: 160,
      render: (v: string | null) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm') : '—'),
    },
    {
      title: 'Mismatch-Row-IDs',
      key: 'mismatchRowIds',
      render: (_: unknown, r: PayloadHashConflictGroup) =>
        r.mismatchRowIds?.length ? (
          <Typography.Text copyable={{ text: r.mismatchRowIds.join('; ') }}>
            {r.mismatchRowIds.length} ID(s)
          </Typography.Text>
        ) : (
          '—'
        ),
    },
    {
      title: 'Blockierende Row-IDs',
      key: 'occupantRowIds',
      render: (_: unknown, r: PayloadHashConflictGroup) =>
        r.occupantRowIds?.length ? (
          <Typography.Text copyable={{ text: r.occupantRowIds.join('; ') }}>
            {r.occupantRowIds.length} ID(s)
          </Typography.Text>
        ) : (
          '—'
        ),
    },
  ];

  const repairableColumns = [
    {
      title: 'Kasse',
      dataIndex: 'cashRegisterId',
      key: 'cashRegisterId',
      width: 120,
      render: (v: string) => <Typography.Text code copyable>{v?.slice(0, 8)}…</Typography.Text>,
    },
    {
      title: 'Canonical Hash',
      dataIndex: 'canonicalHash',
      key: 'canonicalHash',
      width: 120,
      ellipsis: true,
      render: (v: string) => <Typography.Text code copyable>{v?.slice(0, 12)}…</Typography.Text>,
    },
    {
      title: 'Row-ID',
      dataIndex: 'rowId',
      key: 'rowId',
      width: 120,
      render: (v: string) => <Typography.Text code copyable>{v}</Typography.Text>,
    },
    {
      title: 'CreatedAt (UTC)',
      dataIndex: 'createdAtUtc',
      key: 'createdAtUtc',
      width: 160,
      render: (v: string | null) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm') : '—'),
    },
  ];

  const result = data as OfflinePayloadHashAnalyzeResult | undefined;
  const conflictGroups = result?.conflictGroups ?? [];
  const repairableItems = result?.repairableItems ?? [];

  return (
    <>
      <AdminPageHeader
        title="Payload-Hash Konflikte"
        breadcrumbs={[
          { title: 'Dashboard', href: '/dashboard' },
          { title: 'RKSV', href: '/rksv' },
          { title: 'Payload-Hash Konflikte' },
        ]}
        actions={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={() => refetch()}>
              Analyse neu
            </Button>
            <Button
              icon={<DownloadOutlined />}
              loading={downloadCsvMutation.isPending}
              onClick={() => downloadCsvMutation.mutate()}
            >
              CSV exportieren
            </Button>
          </Space>
        }
      />

      {error && (
        <Alert
          type="error"
          message="Analyse fehlgeschlagen"
          description={error instanceof Error ? error.message : 'Unbekannter Fehler'}
          style={{ marginBottom: 16 }}
          showIcon
        />
      )}

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap size="middle">
          <Space>
            <Typography.Text strong>Max. Zeilen:</Typography.Text>
            <InputNumber
              min={1}
              max={100_000}
              value={maxRows}
              onChange={(v) => setMaxRows(v ?? 10_000)}
            />
          </Space>
          <Space>
            <Typography.Text strong>Kasse:</Typography.Text>
            <Select
              placeholder="Alle Kassen"
              allowClear
              value={cashRegisterId ?? undefined}
              onChange={(v) => setCashRegisterId(v ?? undefined)}
              style={{ minWidth: 220 }}
              options={(cashRegisters?.registers ?? []).map((r) => ({
                value: r.id,
                label: r.registerNumber ? `${r.registerNumber} (${r.id.slice(0, 8)}…)` : r.id,
              }))}
            />
          </Space>
          <Button type="primary" onClick={() => refetch()}>
            Analyse ausführen
          </Button>
        </Space>
      </Card>

      {isLoading && !result ? (
        <Card>
          <Spin tip="Analyse läuft…" size="large" />
        </Card>
      ) : result ? (
        <>
          <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
            <Col xs={24} sm={12} md={6}>
              <Card size="small">
                <Statistic title="Gescannt" value={result.scanned} />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card size="small">
                <Statistic title="Mismatch" value={result.runtimeMismatchCount} />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card size="small">
                <Statistic title="Reparierbar (ohne Konflikt)" value={result.repairableNoConflictCount} />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card size="small">
                <Statistic title="Konflikt (übersprungen)" value={result.skippedWouldConflictCount} />
              </Card>
            </Col>
          </Row>

          {result.legacyDataQualityRiskHigh && result.warningMessage && (
            <Alert
              type="warning"
              message="Risiko: Legacy-Datenqualität"
              description={result.warningMessage}
              style={{ marginBottom: 16 }}
              showIcon
            />
          )}

          <Card size="small" title={`Konfliktgruppen (${conflictGroups.length})`} style={{ marginBottom: 16 }}>
            {conflictGroups.length === 0 ? (
              <Typography.Text type="secondary">Keine Konflikte in diesem Scope.</Typography.Text>
            ) : (
              <Table
                columns={conflictColumns}
                dataSource={conflictGroups}
                rowKey={(r) => `${r.cashRegisterId}-${r.canonicalHash}-${r.skipReason}`}
                pagination={{ pageSize: 20, showTotal: (t) => `Gesamt: ${t}` }}
                size="small"
                scroll={{ x: 900 }}
              />
            )}
          </Card>

          <Card size="small" title={`Reparierbare Einträge (${repairableItems.length})`}>
            {repairableItems.length === 0 ? (
              <Typography.Text type="secondary">Keine reparierbaren Einträge in diesem Scope.</Typography.Text>
            ) : (
              <Table
                columns={repairableColumns}
                dataSource={repairableItems}
                rowKey="rowId"
                pagination={{ pageSize: 20, showTotal: (t) => `Gesamt: ${t}` }}
                size="small"
                scroll={{ x: 600 }}
              />
            )}
          </Card>

          <Alert
            type="info"
            message="Nur Ansicht — keine Reparatur"
            description="Reparatur erfolgt weiterhin manuell über API (DryRun zuerst, dann Repair mit SystemCritical). Diese Seite dient der Sichtbarkeit und Triage."
            style={{ marginTop: 16 }}
            showIcon
          />
        </>
      ) : null}
    </>
  );
}
