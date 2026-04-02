/**
 * Çelişkili çapraz kart durumları — operatörün gördüğü anlamlar (model + seçili yüzeyler).
 * Amaç: “teknik başarı” veya “harici OK” tek başına yanlış özgüven üretmesin; üst şiddet önceliği sabit kalsın.
 *
 * Öncelik (yüksekten düşüğe — aynı yüzeyde çakışınca üstteki kazanır):
 * 1) Kanıt merdiveni başlığı: teknik başarısızlık → executionMode uyumsuzluğu → stub/simüle → pg_dump yok → dosya belirsiz →
 *    son drill başarısız → (liste+drill+kanıt tam) strongWithinApi
 * 2) Üst operatör şeridi (`operatorValidity`): executionMode uyarıları → gerçek pg ama kanıt boşluğu → son drill başarısız → …
 * 3) HealthBanner: kritik > uyarı > bilgi (banner modelinde ayrı sıralanır)
 * 4) İlerleme bandı (`BackupRunProgressBanner`): başlıkta simüle > son drill başarısız > recoverability kanıtsız > düz başarı
 */

import { describe, expect, it } from 'vitest';
import '@testing-library/jest-dom';
import React from 'react';
import { render, screen } from '@testing-library/react';
import { buildBackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import {
  bundleLatestSuccessWeakLastKnownGoodProof,
  bundleLatestSuccessFailedLatestDrill,
  bundleSimulatedSuccessHealthyApiCapsReadiness,
  bundleExternalLifecycleOkButRecoverabilityProofGaps,
  bundleProofGapsAndFailedDrill,
  bundleVerificationRunMismatch,
  bundleEmptyEffectiveAdapterKind,
  bundleUnknownAdapterKindPartialDto,
  bundleStaleLatestVersusDetailRunId,
} from '@/features/backup-dr/logic/__tests__/fixtures/backupDrContradictoryBundles';
import {
  mapEvidenceHeadlineToneToAlertType,
  mapExternalCopyVariantToAlertType,
  mapOperatorValidityStripToAlertType,
} from '@/features/backup-dr/logic/backupDrGlancePresentation';
import { SERVER_PIPELINE_PROJECTION_VERSION, resolveBackupPipelineStepsForUi } from '@/features/backup-dr/logic/backupPipelineDerived';
import { BackupRunProgressBanner } from '@/features/backup-dr/components/BackupRunProgressBanner';
import { BackupRunResponseDtoStatus } from '@/api/generated/model';

const eightSteps = [
  { key: 'queued', status: 'success' },
  { key: 'workerRunning', status: 'success' },
  { key: 'dumpComplete', status: 'success' },
  { key: 'artifactCreated', status: 'success' },
  { key: 'artifactVerification', status: 'success' },
  { key: 'manifestCreated', status: 'success' },
  { key: 'externalCopy', status: 'not_required' },
  { key: 'externalChecksum', status: 'not_required' },
] as const;

describe('Backup & DR — çelişkili durum entegrasyonu (operatör görünür semantik)', () => {
  it('latest success + zayıf last-known-good kanıtı: strongWithinApi başlığı seçilmez; şerit kanıt boşluğu', () => {
    const m = buildBackupOperatorTruthModel(bundleLatestSuccessWeakLastKnownGoodProof());
    expect(m.run.technicalSuccess).toBe(true);
    expect(m.recoverability.hasProofGaps).toBe(true);
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.realPgButProofGapsTitle');
    expect(m.evidenceLadder.headlineKey).not.toBe('backupDr.evidence.headline.strongWithinApi');
    expect(m.evidenceLadder.headlineTone).not.toBe('success');
  });

  it('latest success + son tatbikat başarısız: uyarı şeridi + kanıt merdiveni drill başarısız; strongWithinApi yok', () => {
    const m = buildBackupOperatorTruthModel(bundleLatestSuccessFailedLatestDrill());
    expect(m.restore.latestDrillFailed).toBe(true);
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.latestDrillFailedTitle');
    expect(m.operatorValidity?.severity).toBe('warning');
    expect(m.banner.critical.some((x) => x.includes('restoreVerification.drillFailed'))).toBe(true);
    expect(m.evidenceLadder.headlineKey).toBe('backupDr.evidence.headline.latestDrillFailed');
    expect(m.evidenceLadder.headlineTone).toBe('warning');
    expect(m.evidenceLadder.headlineKey).not.toBe('backupDr.evidence.headline.strongWithinApi');
  });

  it('simüle/Fake başarı + API healthy: üst şerit success değil; özet footnote Fake; hazırlık seviyesi düşürülmüş', () => {
    const m = buildBackupOperatorTruthModel(bundleSimulatedSuccessHealthyApiCapsReadiness());
    expect(m.run.simulatedEvidence).toBe(true);
    expect(m.operatorValidity?.severity).not.toBe('success');
    expect(m.summaryPresentation.summaryBackupFootnoteKey).toBe('backupDr.summary.backupHealthFootnoteFake');
    expect(m.summaryPresentation.summaryRestoreFootnoteKey).toBe('backupDr.summary.restoreReadinessFootnoteFake');
    expect(m.restore.apiReadinessLevel).toBe('healthy');
    expect(m.restore.effectiveReadinessLevel).toBe('degraded');
    expect(m.restore.readinessCapped).toBe(true);
  });

  it('harici yaşam döngüsü OK + özet kanıt eksik: harici tek başına “güçlü kanıt” üretmez; şerit kanıt boşluğu', () => {
    const m = buildBackupOperatorTruthModel(bundleExternalLifecycleOkButRecoverabilityProofGaps());
    expect(m.artifact.externalCopyVariant).toBe('externalLifecycleOk');
    expect(m.recoverability.hasProofGaps).toBe(true);
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.realPgButProofGapsTitle');
    expect(m.evidenceLadder.headlineKey).not.toBe('backupDr.evidence.headline.strongWithinApi');
  });

  it('kanıt boşluğu + başarısız drill: şerit önce kanıt boşluğu; banner yine kritik drill; merdiven drill uyarısı', () => {
    const m = buildBackupOperatorTruthModel(bundleProofGapsAndFailedDrill());
    expect(m.operatorValidity?.titleKey).toBe('backupDr.operatorValidity.realPgButProofGapsTitle');
    expect(m.banner.critical.some((x) => x.includes('restoreVerification.drillFailed'))).toBe(true);
    expect(m.evidenceLadder.headlineKey).toBe('backupDr.evidence.headline.latestDrillFailed');
    expect(m.restore.latestDrillFailed).toBe(true);
  });

  it('doğrulama satırı son çalıştırmayla eşleşmiyor: globalVerificationScope mismatch', () => {
    const m = buildBackupOperatorTruthModel(bundleVerificationRunMismatch());
    expect(m.artifact.globalVerificationScope).toBe('mismatch');
  });

  it('boş etkin adaptör türü: PgDump/Fake bayrakları nötr; yanlışlıkla simüle sayılmaz', () => {
    const m = buildBackupOperatorTruthModel(bundleEmptyEffectiveAdapterKind());
    expect(m.executionMode.loaded).toBe(true);
    expect(m.executionMode.effectiveIsSimulatedAdapter).toBe(false);
    expect(m.executionMode.effectiveIsPgDumpAdapter).toBe(false);
  });

  it('bilinmeyen adaptör dizesi + kısmi DTO: Fake çıkarımı yapılmaz; teknik başarı yine de gerçek yol kanıtı değildir', () => {
    const m = buildBackupOperatorTruthModel(bundleUnknownAdapterKindPartialDto());
    expect(m.run.simulatedEvidence).toBe(false);
    expect(m.run.technicalSuccess).toBe(true);
    expect(m.evidenceLadder.headlineKey).not.toBe('backupDr.evidence.headline.stubPipeline');
  });

  it('liste/detay runId penceresi: latest id ≠ detail id iken dosya varlığı detaydan okunur (yanlış önbellek riski)', () => {
    const p = bundleStaleLatestVersusDetailRunId();
    expect(p.latest?.id).not.toBe(p.detailForPipeline?.id);
    const m = buildBackupOperatorTruthModel(p);
    const logical = m.evidenceLadder.steps.find((s) => s.id === 'logical_dump_row');
    expect(logical?.status).toBe('pass');
    expect(m.artifact.globalVerificationScope).toBe('matches_latest_run');
  });

  it('glance: externalLifecycleOk uyarı tonu (metadata); strongWithinApi başarı tonu yeşil Alert değil', () => {
    expect(mapExternalCopyVariantToAlertType('externalLifecycleOk')).toBe('warning');
    expect(mapExternalCopyVariantToAlertType('staging')).toBe('info');
    expect(mapEvidenceHeadlineToneToAlertType('success')).toBe('info');
    expect(mapOperatorValidityStripToAlertType('success')).toBe('info');
    expect(mapOperatorValidityStripToAlertType('warning')).toBe('warning');
  });

  it('sunucu projeksiyonu eksik/uyumsuz + istemci geri dönüş kapalı: resmi adım listesi boş (yanlış tam pipeline yok)', () => {
    const r = resolveBackupPipelineStepsForUi(
      { id: 'a', status: BackupRunResponseDtoStatus.NUMBER_3 } as never,
      {
        id: 'a',
        status: BackupRunResponseDtoStatus.NUMBER_3,
        pipeline: { projectionVersion: '2099-unsupported', steps: [...eightSteps] },
      } as never,
      {},
      { allowClientFallback: false },
    );
    expect(r.source).toBe('client_fallback_blocked');
    expect(r.steps).toHaveLength(0);
  });

  it('desteklenen projeksiyon sürümü: sunucu projeksiyonu kullanılır', () => {
    const r = resolveBackupPipelineStepsForUi(
      { id: 'a', status: BackupRunResponseDtoStatus.NUMBER_3 } as never,
      {
        id: 'a',
        status: BackupRunResponseDtoStatus.NUMBER_3,
        pipeline: { projectionVersion: SERVER_PIPELINE_PROJECTION_VERSION, steps: [...eightSteps] },
      } as never,
      {},
      { allowClientFallback: false },
    );
    expect(r.source).toBe('server_projection');
    expect(r.steps.length).toBeGreaterThan(0);
  });
});

describe('BackupRunProgressBanner — başarı metni “kanıt eksik” ile domine edilmez (görünür uyarı)', () => {
  const t = (k: string) => k;
  const formatDt = (iso: string | undefined | null) => String(iso ?? '');

  it('teknik başarı + recoverabilityNotProven: uyarı tipi, finishedOkUnproven anahtarı', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={
          {
            status: BackupRunResponseDtoStatus.NUMBER_3,
            id: 'x',
            requestedAt: '2026-01-01T00:00:00Z',
          } as never
        }
        isSimulatedExecution={false}
        recoverabilityNotProven
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={formatDt}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(screen.getByText('backupDr.progress.finishedOkUnproven')).toBeTruthy();
  });

  it('öncelik: son drill başarısız + recoverabilityNotProven birlikte — başlık drill (finishedOkLatestDrillFailed)', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={
          {
            status: BackupRunResponseDtoStatus.NUMBER_3,
            id: 'x',
            requestedAt: '2026-01-01T00:00:00Z',
          } as never
        }
        isSimulatedExecution={false}
        recoverabilityNotProven
        latestRestoreDrillFailed
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={formatDt}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(screen.getByText('backupDr.progress.finishedOkLatestDrillFailed')).toBeTruthy();
    expect(screen.queryByText('backupDr.progress.finishedOkUnproven')).not.toBeInTheDocument();
  });

  it('simüle başarı: yeşil “success” Alert yok; finishedSimulatedOk + uyarı sınıfı', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={
          {
            status: BackupRunResponseDtoStatus.NUMBER_3,
            id: 'x',
            requestedAt: '2026-01-01T00:00:00Z',
          } as never
        }
        isSimulatedExecution
        recoverabilityNotProven={false}
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={formatDt}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(screen.getByText('backupDr.progress.finishedSimulatedOk')).toBeTruthy();
    expect(container.querySelector('.ant-alert-success')).not.toBeInTheDocument();
  });
});
