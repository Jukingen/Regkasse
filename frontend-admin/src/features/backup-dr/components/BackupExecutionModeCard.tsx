'use client';

/**
 * Kalıcı yedek çalıştırma modu: istenen / etkin / yapılandırma varsayılanı, Real önkoşul tanıları, onay modalları.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Descriptions,
  List,
  Modal,
  Radio,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd';
import axios from 'axios';
import {
  getBackupExecutionMode,
  putBackupExecutionMode,
  type BackupExecutionModeResponseDto,
} from '@/features/backup-dr/logic/backupExecutionModeApi';
import {
  fakeSwitchNeedsStrongWarning,
  findSelectableRow,
  isRealRequestedNonRunnableState,
  type BackupExecutionModeRadioValue,
} from '@/features/backup-dr/logic/backupDrExecutionModePresentation';
import {
  healthLevelLabel,
  isRealModeSelectableNow,
  parseHypotheticalPgDumpHealthLevel,
  presentRealModeDiagnostics,
  realReadinessSummaryCopy,
} from '@/features/backup-dr/logic/backupRealModeReadinessPresentation';

export type BackupExecutionModeUiValue = BackupExecutionModeRadioValue;

export interface BackupExecutionModeCardProps {
  canManage: boolean;
  t: (k: string, options?: Record<string, string | number>) => string;
  onModeSaved?: () => void;
}

export function BackupExecutionModeCard({ canManage, t, onModeSaved }: BackupExecutionModeCardProps) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [data, setData] = useState<BackupExecutionModeResponseDto | null>(null);
  const [selected, setSelected] = useState<BackupExecutionModeUiValue>('InheritFromConfiguration');
  const [confirmFakeProd, setConfirmFakeProd] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const d = await getBackupExecutionMode();
      setData(d);
      setSelected(initialSelection(d));
    } catch (e) {
      setError(resolveLoadError(e, t));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const dirty = useMemo(() => data != null && initialSelection(data) !== selected, [data, selected]);

  const performSave = useCallback(async () => {
    if (!canManage || !data) return;
    setSaving(true);
    setError(null);
    try {
      const d = await putBackupExecutionMode({
        mode: toPutModeString(selected),
        confirmSimulatedOnlyOperationalRiskInProduction: confirmFakeProd,
      });
      setData(d);
      setSelected(initialSelection(d));
      setConfirmFakeProd(false);
      onModeSaved?.();
    } catch (e) {
      setError(resolvePutError(e, t));
    } finally {
      setSaving(false);
    }
  }, [canManage, confirmFakeProd, data, onModeSaved, selected, t]);

  const requestSave = useCallback(() => {
    if (!canManage || !data || saving || !dirty) return;

    const fakeRow = findSelectableRow(data.selectableModes, 'Fake');
    const wantsFake = selected === 'SimulatedFake';
    const strongFake = fakeSwitchNeedsStrongWarning(selected, fakeRow);

    const title = wantsFake
      ? strongFake
        ? t('backupDr.executionMode.saveConfirmTitleFakeStrong')
        : t('backupDr.executionMode.saveConfirmTitleFake')
      : selected === 'PostgreSqlPgDump'
        ? t('backupDr.executionMode.saveConfirmTitleReal')
        : t('backupDr.executionMode.saveConfirmTitleInherit');

    const content = (
      <Space direction="vertical" size="small" style={{ width: '100%' }}>
        {wantsFake ? (
          <>
            <Typography.Paragraph style={{ marginBottom: 0 }}>
              {strongFake ? t('backupDr.executionMode.saveConfirmBodyFakeStrong') : t('backupDr.executionMode.saveConfirmBodyFake')}
            </Typography.Paragraph>
            {!strongFake ? (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                {t('backupDr.executionMode.saveConfirmBodyFakeDevHint')}
              </Typography.Paragraph>
            ) : null}
          </>
        ) : selected === 'PostgreSqlPgDump' ? (
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            {t('backupDr.executionMode.saveConfirmBodyReal')}
          </Typography.Paragraph>
        ) : (
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            {t('backupDr.executionMode.saveConfirmBodyInherit')}
          </Typography.Paragraph>
        )}
      </Space>
    );

    Modal.confirm({
      title,
      content,
      okText: t('backupDr.executionMode.saveConfirmOk'),
      cancelText: t('common.buttons.cancel'),
      okButtonProps: wantsFake ? { danger: strongFake } : undefined,
      width: 520,
      onOk: () => performSave(),
    });
  }, [canManage, data, dirty, performSave, saving, selected, t]);

  const inheritSelectable = findSelectableRow(data?.selectableModes, 'UseConfigurationDefault')?.selectable !== false;
  const fakeSelectable = findSelectableRow(data?.selectableModes, 'Fake')?.selectable === true;
  const realSelectable = findSelectableRow(data?.selectableModes, 'RealPgDump')?.selectable === true;

  const realReadiness = useMemo(() => {
    if (!data) return null;
    const hypoLevel = parseHypotheticalPgDumpHealthLevel(data.hypotheticalPgDumpHealthLevel);
    const selectable = isRealModeSelectableNow(data);
    return {
      hypoLevel,
      selectable,
      summary: realReadinessSummaryCopy(hypoLevel, selectable, t),
      diagnostics: presentRealModeDiagnostics(data.realModeBlockingDiagnostics, t),
    };
  }, [data, t]);

  const showRealRunnableTag = Boolean(data?.effectiveUserFacingMode === 'RealPgDump');
  const realRunnable = Boolean(showRealRunnableTag && data?.effectiveModeRunnable);

  return (
    <Card title={t('backupDr.executionMode.title')} size="small">
      {loading ? (
        <Spin />
      ) : (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          {error && (
            <Alert type="error" showIcon message={error} closable onClose={() => setError(null)} />
          )}

          {data && isRealRequestedNonRunnableState(data) ? (
            <Alert
              type="warning"
              showIcon
              message={t('backupDr.executionMode.realSavedButBlockedTitle')}
              description={
                <div>
                  <Typography.Paragraph style={{ marginBottom: 8 }}>
                    {t('backupDr.executionMode.realSavedButBlockedBody')}
                  </Typography.Paragraph>
                  <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                    {t('backupDr.executionMode.noFakeFallbackExplanation')}
                  </Typography.Paragraph>
                </div>
              }
            />
          ) : null}

          {data && !isRealRequestedNonRunnableState(data) && !data.effectiveModeRunnable ? (
            <Alert
              type="warning"
              showIcon
              message={t('backupDr.executionMode.notRunnableTitle')}
              description={
                <ul style={{ marginBottom: 0, paddingLeft: 18 }}>
                  {(data.blockers ?? []).map((b) => (
                    <li key={b}>{b}</li>
                  ))}
                </ul>
              }
            />
          ) : null}

          {data && realReadiness ? (
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Alert
                type={realReadiness.summary.alertType}
                showIcon
                message={realReadiness.summary.title}
                description={realReadiness.summary.description}
              />
              <div>
                <Space align="center" wrap style={{ marginBottom: 8 }}>
                  <Typography.Text strong>{t('backupDr.executionMode.realReadiness.panelTitle')}</Typography.Text>
                  <Tag>{healthLevelLabel(realReadiness.hypoLevel, t)}</Tag>
                  {realReadiness.selectable ? (
                    <Tag color="success">{t('backupDr.executionMode.realReadiness.realRowSelectableYes')}</Tag>
                  ) : (
                    <Tag color="error">{t('backupDr.executionMode.realReadiness.realRowSelectableNo')}</Tag>
                  )}
                </Space>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                  {t('backupDr.executionMode.realReadiness.footnoteApi')}
                </Typography.Paragraph>
                {realReadiness.diagnostics.length > 0 ? (
                  <List
                    size="small"
                    bordered
                    dataSource={realReadiness.diagnostics}
                    renderItem={(item) => (
                      <List.Item style={{ alignItems: 'flex-start' }}>
                        <Space direction="vertical" size={4} style={{ width: '100%' }}>
                          <Space wrap size={6}>
                            <Tag color={item.tier === 'blocking' ? 'red' : 'gold'}>
                              {item.tier === 'blocking'
                                ? t('backupDr.executionMode.realReadiness.tier.blocking')
                                : t('backupDr.executionMode.realReadiness.tier.advisory')}
                            </Tag>
                            <Tag>{t(`backupDr.executionMode.realReadiness.category.${item.category}`)}</Tag>
                            <Typography.Text type="secondary" code style={{ fontSize: 11 }}>
                              {item.code}
                            </Typography.Text>
                          </Space>
                          <Typography.Text strong>{item.title}</Typography.Text>
                          <Typography.Paragraph style={{ marginBottom: 0 }}>{item.action}</Typography.Paragraph>
                          {item.configKeys.length > 0 ? (
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {t('backupDr.executionMode.realReadiness.configKeysLabel')}{' '}
                              <Typography.Text code style={{ fontSize: 11 }}>
                                {item.configKeys.join(', ')}
                              </Typography.Text>
                            </Typography.Text>
                          ) : null}
                          {item.serverMessage && item.serverMessage !== item.action ? (
                            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 11 }}>
                              {t('backupDr.executionMode.realReadiness.serverDetailLabel')} {item.serverMessage}
                            </Typography.Paragraph>
                          ) : null}
                        </Space>
                      </List.Item>
                    )}
                  />
                ) : (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('backupDr.executionMode.realReadiness.noDiagnosticsListed')}
                  </Typography.Text>
                )}
              </div>
            </Space>
          ) : null}

          <Alert
            type="info"
            showIcon
            message={t('backupDr.executionMode.meaningTitle')}
            description={
              <ul style={{ marginBottom: 0, paddingLeft: 18 }}>
                <li>{t('backupDr.executionMode.meaningFake')}</li>
                <li>{t('backupDr.executionMode.meaningReal')}</li>
              </ul>
            }
          />

          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('backupDr.executionMode.intro')}
          </Typography.Paragraph>

          {data ? (
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label={t('backupDr.executionMode.labelRequested')}>
                <Space wrap>
                  <Tag color="blue">{labelUserFacingMode(data.requestedUserFacingMode, t)}</Tag>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    ({data.storedMode})
                  </Typography.Text>
                </Space>
              </Descriptions.Item>
              <Descriptions.Item label={t('backupDr.executionMode.labelEffective')}>
                <Space wrap>
                  <Tag color={data.effectiveModeRunnable ? 'green' : 'orange'}>
                    {labelUserFacingMode(data.effectiveUserFacingMode, t)}
                  </Tag>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('backupDr.executionMode.adapterInternal')}: {data.effectiveExecutionAdapterKind}
                  </Typography.Text>
                  {showRealRunnableTag ? (
                    <Tag color={realRunnable ? 'success' : 'warning'}>
                      {realRunnable
                        ? t('backupDr.executionMode.realRunnableYes')
                        : t('backupDr.executionMode.realRunnableNo')}
                    </Tag>
                  ) : null}
                </Space>
              </Descriptions.Item>
              <Descriptions.Item label={t('backupDr.executionMode.labelConfigDefault')}>
                <Space wrap>
                  <Tag>{labelUserFacingMode(data.configurationDefaultUserFacingMode, t)}</Tag>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('backupDr.executionMode.adapterInternal')}: {data.configurationExecutionAdapterKind}
                  </Typography.Text>
                </Space>
              </Descriptions.Item>
            </Descriptions>
          ) : null}

          {data?.effectiveModeResolutionSummaryEnglish ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
              <Typography.Text strong>{t('backupDr.executionMode.resolutionSummaryLabel')}</Typography.Text>{' '}
              <Typography.Text code style={{ fontSize: 12, whiteSpace: 'pre-wrap' }}>
                {data.effectiveModeResolutionSummaryEnglish}
              </Typography.Text>
            </Typography.Paragraph>
          ) : null}

          {data?.recommendedFallbackUserFacingMode ? (
            <Alert
              type="info"
              showIcon
              message={t('backupDr.executionMode.recommendedFallbackTitle')}
              description={t('backupDr.executionMode.recommendedFallbackBody', {
                mode: labelUserFacingMode(data.recommendedFallbackUserFacingMode, t),
              })}
            />
          ) : null}

          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('backupDr.executionMode.saveRejectedHint')}
          </Typography.Text>

          {/* Ant Design: alt Radio’larda açık disabled={false}, grup seviyesindeki disabled’ı geçersiz kılar; seçilebilir satırlarda prop’u bırak. */}
          <Radio.Group
            value={selected}
            onChange={(e) => setSelected(e.target.value as BackupExecutionModeUiValue)}
            disabled={!canManage || saving}
          >
            <Space direction="vertical">
              <Radio value="InheritFromConfiguration" disabled={inheritSelectable ? undefined : true}>
                {t('backupDr.executionMode.optionInherit')}
              </Radio>
              <Radio value="SimulatedFake" disabled={fakeSelectable ? undefined : true}>
                {t('backupDr.executionMode.optionFake')}
              </Radio>
              {!fakeSelectable && findSelectableRow(data?.selectableModes, 'Fake')?.blockReason ? (
                <Typography.Text type="secondary" style={{ fontSize: 12, marginLeft: 24 }}>
                  {findSelectableRow(data?.selectableModes, 'Fake')?.blockReason}
                </Typography.Text>
              ) : null}
              <Radio value="PostgreSqlPgDump" disabled={realSelectable ? undefined : true}>
                {t('backupDr.executionMode.optionPgDump')}
              </Radio>
              {!realSelectable && findSelectableRow(data?.selectableModes, 'RealPgDump')?.blockReason ? (
                <Typography.Text type="secondary" style={{ fontSize: 12, marginLeft: 24 }}>
                  {findSelectableRow(data?.selectableModes, 'RealPgDump')?.blockReason}
                </Typography.Text>
              ) : null}
            </Space>
          </Radio.Group>

          <Typography.Text type="secondary">{modeHelpText(selected, t)}</Typography.Text>

          {selected === 'SimulatedFake' && canManage ? (
            <Checkbox
              checked={confirmFakeProd}
              disabled={saving}
              onChange={(e) => setConfirmFakeProd(e.target.checked)}
            >
              {t('backupDr.executionMode.confirmFakeProduction')}
            </Checkbox>
          ) : null}

          <div>
            <Button
              type="primary"
              onClick={() => requestSave()}
              loading={saving}
              disabled={!canManage || !dirty}
            >
              {t('backupDr.executionMode.save')}
            </Button>
            {!canManage ? (
              <Typography.Text type="secondary" style={{ marginLeft: 12 }}>
                {t('backupDr.executionMode.viewOnlyHint')}
              </Typography.Text>
            ) : null}
          </div>
        </Space>
      )}
    </Card>
  );
}

function labelUserFacingMode(mode: string | undefined, t: (k: string) => string): string {
  const m = (mode ?? '').trim();
  if (m === 'Fake') return t('backupDr.executionMode.userFacing.fake');
  if (m === 'RealPgDump') return t('backupDr.executionMode.userFacing.realPgDump');
  if (m === 'UseConfigurationDefault') return t('backupDr.executionMode.userFacing.useConfig');
  if (m === 'ProductionStub') return t('backupDr.executionMode.userFacing.productionStub');
  return m || '—';
}

function initialSelection(d: BackupExecutionModeResponseDto): BackupExecutionModeUiValue {
  const u = (d.requestedUserFacingMode ?? '').trim();
  if (u === 'Fake') return 'SimulatedFake';
  if (u === 'RealPgDump') return 'PostgreSqlPgDump';
  if (u === 'UseConfigurationDefault') return 'InheritFromConfiguration';
  const x = (d.storedMode ?? '').trim();
  if (x === 'SimulatedFake' || x === 'PostgreSqlPgDump' || x === 'InheritFromConfiguration') return x;
  return 'InheritFromConfiguration';
}

function toPutModeString(mode: BackupExecutionModeUiValue): string {
  switch (mode) {
    case 'InheritFromConfiguration':
      return 'UseConfigurationDefault';
    case 'SimulatedFake':
      return 'Fake';
    case 'PostgreSqlPgDump':
      return 'RealPgDump';
    default:
      return 'UseConfigurationDefault';
  }
}

function modeHelpText(mode: BackupExecutionModeUiValue, t: (k: string) => string): string {
  switch (mode) {
    case 'InheritFromConfiguration':
      return t('backupDr.executionMode.helpInherit');
    case 'SimulatedFake':
      return t('backupDr.executionMode.helpFake');
    case 'PostgreSqlPgDump':
      return t('backupDr.executionMode.helpPgDump');
    default:
      return '';
  }
}

function resolveLoadError(err: unknown, t: (k: string) => string): string {
  if (axios.isAxiosError(err)) {
    if (err.response?.status === 403) return t('backupDr.executionMode.errors.forbidden');
    if (err.response?.status === 401) return t('backupDr.executionMode.errors.unauthorized');
  }
  return t('backupDr.executionMode.errors.loadFailed');
}

function resolvePutError(err: unknown, t: (k: string) => string): string {
  if (axios.isAxiosError(err)) {
    const code = (err.response?.data as { code?: string } | undefined)?.code;
    if (code === 'BACKUP_SIMULATED_FAKE_CONFIRMATION_REQUIRED')
      return t('backupDr.executionMode.errors.confirmFakeRequired');
    if (code === 'BACKUP_SIMULATED_FAKE_FORBIDDEN_PRODUCTION')
      return t('backupDr.executionMode.errors.fakeForbiddenProduction');
    if (code === 'BACKUP_PG_DUMP_PREREQUISITES_UNHEALTHY')
      return t('backupDr.executionMode.errors.pgDumpUnhealthy');
    if (err.response?.status === 403) return t('backupDr.executionMode.errors.forbidden');
    if (err.response?.status === 401) return t('backupDr.executionMode.errors.unauthorized');
  }
  return t('backupDr.executionMode.errors.saveFailed');
}
