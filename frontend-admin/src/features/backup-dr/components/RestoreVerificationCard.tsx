'use client';

/**
 * Son restore doğrulama (drill) özeti — yedek pipeline’ından ayrı blok.
 * PG_RESTORE_LIST_FAILED: Fake stub beklenen davranış ile gerçek pg_dump hattı riski ayrı anlatılır.
 */

import React, { useMemo } from 'react';
import { Alert, Card, Descriptions, Tag, Typography } from 'antd';
import type { RestoreVerificationRunResponseDto } from '@/api/generated/model';
import { RestoreVerificationRunResponseDtoStatus } from '@/api/generated/model';
import {
  PG_RESTORE_LIST_FAILED,
  interpretPgRestoreListFailure,
  pgRestoreListFailureKindToCardAlertKeys,
  pgRestoreListFailureKindToStatusLabelKey,
  pgRestoreListFailureKindToTagColor,
} from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';

export interface RestoreVerificationCardProps {
  run: RestoreVerificationRunResponseDto | undefined | null;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  restoreStatusTagColor: (status: number) => string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  dumpInspectionTriState: (rr: RestoreVerificationRunResponseDto | undefined | null) => boolean | undefined;
  /** Yapılandırma Fake/simüle ise DetailsJson olmadan da stub açıklaması gösterilebilir. */
  isSimulatedBackupPipeline: boolean;
  /** execution-mode: worker PgDump hedefliyor ama profil şu an runnable değil — drill yorumu için bağlam. */
  backupWorkerRealProfileBlocked?: boolean;
  t: (k: string) => string;
}

export function RestoreVerificationCard({
  run,
  formatDt,
  formatLocale,
  restoreStatusTagColor,
  restoreStatusLabel,
  dumpInspectionTriState,
  isSimulatedBackupPipeline,
  backupWorkerRealProfileBlocked,
  t,
}: RestoreVerificationCardProps) {
  const listFailureInterp = useMemo(() => {
    if (!run || run.failureCode !== PG_RESTORE_LIST_FAILED) return null;
    return interpretPgRestoreListFailure({
      run,
      isSimulatedPipelineHeuristic: isSimulatedBackupPipeline,
    });
  }, [run, isSimulatedBackupPipeline]);

  const isFakeStubListFailure = listFailureInterp?.kind === 'fake_stub_expected';

  const dumpInspectionLabel = useMemo(() => {
    if (!run) return '—';
    const tri = dumpInspectionTriState(run);
    if (tri === undefined) return '—';
    if (tri === true) return t('backupDr.triState.ok');
    if (isFakeStubListFailure) return t('backupDr.triState.dumpInspectionNotApplicableStub');
    return t('backupDr.triState.fail');
  }, [dumpInspectionTriState, run, isFakeStubListFailure, t]);

  const statusTagColorResolved = useMemo(() => {
    if (!run) return restoreStatusTagColor(-1);
    if (run.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3 && listFailureInterp) {
      return pgRestoreListFailureKindToTagColor(listFailureInterp.kind);
    }
    return restoreStatusTagColor(run.status ?? -1);
  }, [restoreStatusTagColor, run, listFailureInterp]);

  const statusLabel = useMemo(() => {
    if (!run) return restoreStatusLabel(undefined, t);
    if (run.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3 && listFailureInterp) {
      return t(pgRestoreListFailureKindToStatusLabelKey(listFailureInterp.kind));
    }
    return restoreStatusLabel(run.status, t);
  }, [restoreStatusLabel, run, listFailureInterp, t]);

  const failureTypography = useMemo(() => {
    if (!listFailureInterp) return { code: undefined, detail: undefined };
    if (listFailureInterp.kind === 'fake_stub_expected') {
      return { code: 'secondary' as const, detail: 'secondary' as const };
    }
    const c = pgRestoreListFailureKindToCardAlertKeys(listFailureInterp.kind);
    if (c.tone === 'warning') return { code: 'warning' as const, detail: 'warning' as const };
    return { code: 'danger' as const, detail: 'danger' as const };
  }, [listFailureInterp]);

  return (
    <Card title={t('backupDr.restoreVerification.title')} size="small">
      <Typography.Paragraph type="secondary">{t('backupDr.restoreVerification.explanation')}</Typography.Paragraph>
      {backupWorkerRealProfileBlocked ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.restoreVerification.realProfileBlockedTitle')}
          description={t('backupDr.restoreVerification.realProfileBlockedBody')}
        />
      ) : null}
      {isSimulatedBackupPipeline ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.restoreVerification.fakePipeline.sectionContext')}
        </Typography.Paragraph>
      ) : null}
      {!run ? (
        <Typography.Text type="secondary">{t('backupDr.restoreVerification.none')}</Typography.Text>
      ) : (
        <>
          {run.status === RestoreVerificationRunResponseDtoStatus.NUMBER_3 ? (
            listFailureInterp ? (
              (() => {
                const card = pgRestoreListFailureKindToCardAlertKeys(listFailureInterp.kind);
                const alertType = card.tone === 'error' ? 'error' : card.tone === 'warning' ? 'warning' : 'info';
                return (
                  <Alert
                    type={alertType}
                    showIcon
                    style={{ marginBottom: 12 }}
                    title={t(card.titleKey)}
                    description={
                      <div>
                        <Typography.Paragraph
                          style={{ marginBottom: 8 }}
                          type={card.tone === 'error' ? 'danger' : undefined}
                        >
                          {t(card.bodyKey)}
                        </Typography.Paragraph>
                        <Typography.Text type="secondary" style={{ fontSize: 12, whiteSpace: 'pre-wrap' }}>
                          {t('backupDr.restoreVerification.fakePipeline.technicalDetailLabel')}:{' '}
                          {[run.failureCode, (run.failureDetail ?? '').trim()].filter(Boolean).join(' — ') || '—'}
                        </Typography.Text>
                      </div>
                    }
                  />
                );
              })()
            ) : (
              <Alert
                type="error"
                showIcon
                style={{ marginBottom: 12 }}
                title={t('backupDr.restoreVerification.drillFailedProminent')}
                description={
                  <Typography.Text type="danger" style={{ whiteSpace: 'pre-wrap' }}>
                    {[run.failureCode, (run.failureDetail ?? '').trim()].filter(Boolean).join(' — ') || '—'}
                  </Typography.Text>
                }
              />
            )
          ) : null}
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('backupDr.table.status')}>
              <Tag color={statusTagColorResolved}>{statusLabel}</Tag>
            </Descriptions.Item>
            {run.failureCode || run.failureDetail ? (
              <>
                <Descriptions.Item label={t('backupDr.restoreVerification.failureCode')}>
                  <Typography.Text type={failureTypography.code}>{run.failureCode ?? '—'}</Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('backupDr.restoreVerification.failureDetail')} span={1}>
                  <Typography.Text
                    type={failureTypography.detail}
                    style={{ whiteSpace: 'pre-wrap' }}
                  >
                    {(run.failureDetail ?? '').trim() || '—'}
                  </Typography.Text>
                </Descriptions.Item>
              </>
            ) : null}
            <Descriptions.Item label={t('backupDr.restoreVerification.block.dumpInspection')}>
              {dumpInspectionLabel}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerification.block.restoreAttempt')}>
              {!run.restoreAttemptExecuted
                ? t('backupDr.restoreAttempt.notRun')
                : run.restoreAttemptPassed === true
                  ? t('backupDr.triState.ok')
                  : run.restoreAttemptPassed === false
                    ? t('backupDr.triState.fail')
                    : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerification.fiscalSql')}>
              {run.fiscalSqlSkipped
                ? `${t('backupDr.restoreVerification.skipped')} (${run.fiscalSqlSkipReason ?? '—'})`
                : run.fiscalSqlPassed === true
                  ? t('backupDr.triState.ok')
                  : run.fiscalSqlPassed === false
                    ? t('backupDr.triState.fail')
                    : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.restoreVerification.integrity')}>
              {run.integrityChecksPassed == null
                ? '—'
                : `${run.integrityChecksPassed ? t('backupDr.triState.ok') : t('backupDr.triState.issues')} (${run.integrityScope ?? '—'})`}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.completed')}>
              {formatDt(run.completedAt, formatLocale)}
            </Descriptions.Item>
          </Descriptions>
        </>
      )}
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        {t('backupDr.restoreVerification.strongerThanArtifact')}
      </Typography.Paragraph>
    </Card>
  );
}
