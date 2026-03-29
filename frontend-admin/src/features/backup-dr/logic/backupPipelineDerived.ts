/**
 * Sunucu `pipeline` snapshot’ı geçerliyse tek doğruluk kaynağı odur.
 * İstemci türetimi yalnızca `isBackupPipelineClientFallbackEnabled()` ile açıkça etkinleştirildiğinde kullanılmalıdır.
 */

import type {
  BackupArtifactPipelinePolicyResponseDto,
  BackupArtifactResponseDto,
  BackupPipelineSnapshotDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
} from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';
import { BackupArtifactResponseDtoLifecycleState } from '@/api/generated/model/backupArtifactResponseDtoLifecycleState';

export type DerivedPipelineStepState = 'pending' | 'running' | 'success' | 'failed' | 'skipped' | 'degraded';

export type DerivedPipelineStepId =
  | 'queued'
  | 'workerRunning'
  | 'dumpComplete'
  | 'artifactCreated'
  | 'artifactVerification'
  | 'manifestCreated'
  | 'externalCopy'
  | 'externalChecksum';

export interface DerivedPipelineStep {
  id: DerivedPipelineStepId;
  state: DerivedPipelineStepState;
  /** i18n anahtarı: backupDr.pipelineSteps.<id>.title */
  titleKey: string;
  /** i18n anahtarı: backupDr.pipelineSteps.hint.<id> veya state bazlı opsiyonel */
  hintKey?: string;
}

/** SYNC: KasseAPI_Final.Services.Backup.BackupPipelineProjector.ProjectionVersion */
export const SERVER_PIPELINE_PROJECTION_VERSION = '2026-03-28';

const SERVER_PIPELINE_STEP_COUNT = 8;

/** SYNC: BackupPipelineProjector.PipelineStepKeysOrdered (sıra ve anahtarlar birebir). */
export const SERVER_PIPELINE_STEP_KEYS_ORDERED: readonly DerivedPipelineStepId[] = [
  'queued',
  'workerRunning',
  'dumpComplete',
  'artifactCreated',
  'artifactVerification',
  'manifestCreated',
  'externalCopy',
  'externalChecksum',
];

function isDerivedPipelineStepId(key: string | undefined): key is DerivedPipelineStepId {
  return (
    key === 'queued' ||
    key === 'workerRunning' ||
    key === 'dumpComplete' ||
    key === 'artifactCreated' ||
    key === 'artifactVerification' ||
    key === 'manifestCreated' ||
    key === 'externalCopy' ||
    key === 'externalChecksum'
  );
}

/** API `not_required` → stepper’da skipped (policy dışı adım). */
export function mapServerPipelineStatus(status: string | undefined): DerivedPipelineStepState {
  if (status === 'not_required') return 'skipped';
  if (
    status === 'pending' ||
    status === 'running' ||
    status === 'success' ||
    status === 'failed' ||
    status === 'skipped' ||
    status === 'degraded'
  ) {
    return status;
  }
  return 'pending';
}

/**
 * Backend `pipeline` snapshot → mevcut stepper modeli (i18n anahtarları aynı).
 * Eski API veya eksik adım: null döner, caller `deriveBackupPipelineSteps` kullanır.
 */
export function pipelineSnapshotToDerivedSteps(
  snapshot: BackupPipelineSnapshotDto | undefined | null,
): DerivedPipelineStep[] | null {
  if (!snapshot?.steps?.length || snapshot.steps.length !== SERVER_PIPELINE_STEP_COUNT) return null;
  const out: DerivedPipelineStep[] = [];
  for (let i = 0; i < SERVER_PIPELINE_STEP_COUNT; i++) {
    const s = snapshot.steps[i];
    const expectedKey = SERVER_PIPELINE_STEP_KEYS_ORDERED[i];
    if (!s || s.key !== expectedKey || !isDerivedPipelineStepId(s.key)) return null;
    out.push({
      id: s.key,
      state: mapServerPipelineStatus(s.status),
      titleKey: `backupDr.pipelineSteps.${s.key}.title`,
      hintKey: `backupDr.pipelineSteps.${s.key}.hint`,
    });
  }
  return out;
}

const RUN_QUEUED = 0;
const RUN_RUNNING = 1;
const RUN_AWAIT_VERIFY = 2;
const RUN_SUCCEEDED = 3;
const RUN_FAILED = 4;
const RUN_VERIFY_FAILED = 5;
const RUN_CANCELLED = 6;

function artifactByType(artifacts: BackupArtifactResponseDto[] | null | undefined, type: number): BackupArtifactResponseDto | undefined {
  return artifacts?.find((a) => a.artifactType === type);
}

function pickPrimaryVerification(
  runId: string | undefined,
  list: BackupVerificationResponseDto[] | null | undefined,
): BackupVerificationResponseDto | undefined {
  if (!list?.length) return undefined;
  const forRun = runId ? list.filter((v) => !v.backupRunId || v.backupRunId === runId) : list;
  const pool = forRun.length ? forRun : list;
  return [...pool].sort((a, b) => {
    const ca = a.completedAt ? Date.parse(a.completedAt) : 0;
    const cb = b.completedAt ? Date.parse(b.completedAt) : 0;
    return cb - ca;
  })[0];
}

function externalPipelineExpected(policy: BackupArtifactPipelinePolicyResponseDto | undefined): boolean {
  return Boolean(policy?.willRunExternalArchiveAfterStagingVerificationWhenEligible && policy?.externalArchiveRootConfigured);
}

/**
 * Tek bir yedek çalıştırması için sekiz adımın durumunu hesaplar.
 * @param run — özet satır (latest); @param detail — runs/{id} çocukları (artifacts, verifications)
 */
export function deriveBackupPipelineSteps(
  run: BackupRunResponseDto | undefined | null,
  detail: BackupRunResponseDto | undefined | null,
  policy: BackupArtifactPipelinePolicyResponseDto | undefined,
): DerivedPipelineStep[] {
  if (!run?.id) {
    return [];
  }

  const st = run.status ?? RUN_QUEUED;
  const artifacts = detail?.artifacts ?? run.artifacts ?? [];
  const verifications = detail?.verifications ?? run.verifications ?? [];
  const logical = artifactByType(artifacts, BackupArtifactResponseDtoArtifactType.NUMBER_0);
  const manifest = artifactByType(artifacts, BackupArtifactResponseDtoArtifactType.NUMBER_4);
  const pv = pickPrimaryVerification(run.id, verifications);
  const wantExternal = externalPipelineExpected(policy);

  const terminalFail = st === RUN_FAILED;
  const terminalVerifyFail = st === RUN_VERIFY_FAILED;
  const terminalOk = st === RUN_SUCCEEDED;
  const cancelled = st === RUN_CANCELLED;

  const step = (id: DerivedPipelineStepId, state: DerivedPipelineStepState): DerivedPipelineStep => ({
    id,
    state,
    titleKey: `backupDr.pipelineSteps.${id}.title`,
    hintKey: `backupDr.pipelineSteps.${id}.hint`,
  });

  if (cancelled) {
    return [
      step('queued', 'success'),
      step('workerRunning', 'skipped'),
      step('dumpComplete', 'skipped'),
      step('artifactCreated', 'skipped'),
      step('artifactVerification', 'skipped'),
      step('manifestCreated', 'skipped'),
      step('externalCopy', 'skipped'),
      step('externalChecksum', 'skipped'),
    ];
  }

  // 1 — Kuyruk
  let q: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED) q = 'running';
  else q = 'success';

  // 2 — Worker / yürütme
  let wr: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED) wr = 'pending';
  else if (st === RUN_RUNNING) wr = 'running';
  else if (terminalFail && !logical) wr = 'failed';
  else if (st >= RUN_AWAIT_VERIFY || terminalOk || terminalVerifyFail) wr = 'success';
  else wr = 'pending';

  // 3 — pg_dump tamam (mantıksal: yürütme bitti, doğrulama aşamasına geçilebilir)
  let dmp: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED || st === RUN_RUNNING) dmp = 'pending';
  else if (terminalFail && !logical) dmp = 'failed';
  else if (st >= RUN_AWAIT_VERIFY || terminalOk || terminalVerifyFail) dmp = 'success';
  else dmp = 'pending';

  // 4 — Artefakt (logical dump)
  let art: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED || st === RUN_RUNNING) art = 'pending';
  else if (logical) art = 'success';
  else if (terminalFail || terminalVerifyFail) art = 'failed';
  else art = 'pending';

  // 5 — Artefakt doğrulama (checksum / staging doğrulama satırı)
  let av: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED || st === RUN_RUNNING) av = 'pending';
  else if (!pv) {
    // Terminal run without verification row: do not show "running" (false activity).
    if (st === RUN_AWAIT_VERIFY) av = 'running';
    else av = 'pending';
  } else if (pv.status === 0) av = st === RUN_AWAIT_VERIFY ? 'running' : 'pending';
  else if (pv.status === 1) av = 'success';
  else if (pv.status === 2) av = 'failed';
  else av = 'pending';

  // 6 — Manifest
  let mf: DerivedPipelineStepState = 'pending';
  if (st === RUN_QUEUED || st === RUN_RUNNING) mf = 'pending';
  else if (manifest) mf = 'success';
  else if (terminalOk && !manifest) mf = 'pending';
  else if (terminalFail || terminalVerifyFail) mf = 'failed';
  else mf = 'pending';

  const ls = logical?.lifecycleState;
  const stagingVerified = ls === BackupArtifactResponseDtoLifecycleState.NUMBER_1;
  const externalOk = ls === BackupArtifactResponseDtoLifecycleState.NUMBER_2;
  const externalBad = ls === BackupArtifactResponseDtoLifecycleState.NUMBER_3;
  /** Başarılı run ama harici arşiv bu koşuda tetiklenmedi (StagingVerified’da kaldı). */
  const externalNotRunThisRun = Boolean(wantExternal && logical && stagingVerified && terminalOk);

  // 7 — Harici kopya
  let ex: DerivedPipelineStepState = 'pending';
  if (!wantExternal) ex = 'skipped';
  else if (!logical) ex = 'pending';
  else if (externalBad) ex = 'degraded';
  else if (externalOk) ex = 'success';
  else if (externalNotRunThisRun) ex = 'skipped';
  else if (st === RUN_QUEUED || st === RUN_RUNNING || st === RUN_AWAIT_VERIFY) ex = 'pending';
  else if (stagingVerified && terminalVerifyFail) ex = 'degraded';
  else if (terminalOk || terminalVerifyFail) ex = 'pending';
  else ex = 'pending';

  // 8 — Harici checksum (ExternalVerified = hedefte hash eşleşti)
  let ec: DerivedPipelineStepState = 'pending';
  if (!wantExternal) ec = 'skipped';
  else if (!logical) ec = 'pending';
  else if (externalOk) ec = 'success';
  else if (externalBad) ec = 'failed';
  else if (externalNotRunThisRun) ec = 'skipped';
  else if (stagingVerified && terminalVerifyFail) ec = 'degraded';
  else ec = 'pending';

  return [
    step('queued', q),
    step('workerRunning', wr),
    step('dumpComplete', dmp),
    step('artifactCreated', art),
    step('artifactVerification', av),
    step('manifestCreated', mf),
    step('externalCopy', ex),
    step('externalChecksum', ec),
  ];
}

export function formatRunDurationMs(requestedAt?: string, completedAt?: string | null): number | undefined {
  if (!requestedAt || !completedAt) return undefined;
  const a = Date.parse(requestedAt);
  const b = Date.parse(completedAt);
  if (Number.isNaN(a) || Number.isNaN(b) || b < a) return undefined;
  return b - a;
}

export function sumLogicalDumpBytes(artifacts: BackupArtifactResponseDto[] | null | undefined): number | undefined {
  const d = artifacts?.find((a) => a.artifactType === BackupArtifactResponseDtoArtifactType.NUMBER_0);
  const n = d?.byteSize;
  if (n === undefined || n === null) return undefined;
  return n;
}

/** Sunucu snapshot’ı geçerliyse onu; aksi halde (izin varsa) istemci türetmesi. */
export type BackupPipelineUiSource = 'server_projection' | 'client_fallback' | 'client_fallback_blocked';

export interface ResolvedBackupPipelineUi {
  steps: DerivedPipelineStep[];
  source: BackupPipelineUiSource;
  /** Şekil uygun ama projectionVersion bu UI sürümüyle eşleşmiyor. */
  projectionVersionMismatch: boolean;
}

export function resolveBackupPipelineStepsForUi(
  latest: BackupRunResponseDto | null | undefined,
  detail: BackupRunResponseDto | null | undefined,
  policy: BackupArtifactPipelinePolicyResponseDto | undefined,
  options: { allowClientFallback: boolean },
): ResolvedBackupPipelineUi {
  const snap = detail?.pipeline ?? latest?.pipeline;
  const parsed = pipelineSnapshotToDerivedSteps(snap);
  const version = snap?.projectionVersion?.trim();
  const versionOk = !version || version === SERVER_PIPELINE_PROJECTION_VERSION;

  if (parsed && versionOk) {
    return { steps: parsed, source: 'server_projection', projectionVersionMismatch: false };
  }

  const projectionVersionMismatch = Boolean(parsed && !versionOk);

  if (options.allowClientFallback) {
    return {
      steps: deriveBackupPipelineSteps(latest, detail, policy),
      source: 'client_fallback',
      projectionVersionMismatch,
    };
  }

  return {
    steps: [],
    source: 'client_fallback_blocked',
    projectionVersionMismatch,
  };
}
