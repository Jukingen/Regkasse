/**
 * Admin seçilebilir yedek çalıştırma modu — operatör-doğruluk modeli için tek boyutlu özet (requested / effective / runnable / blokaj).
 */
import { isSimulatedBackupAdapterKind } from '@/features/backup-dr/logic/backupDrMappers';
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

/** Mod API’si yok veya yüklenmediğinde kullanılan boş özet. */
export const unloadedBackupExecutionModeTruth: BackupExecutionModeTruth = {
  loaded: false,
  requestedUserFacingMode: '',
  effectiveUserFacingMode: '',
  configurationDefaultUserFacingMode: '',
  effectiveExecutionAdapterKind: '',
  configurationExecutionAdapterKind: '',
  effectiveIsSimulatedAdapter: false,
  effectiveIsPgDumpAdapter: false,
  effectiveModeRunnable: false,
  requestedRealButBlocked: false,
  recommendedFallbackUserFacingMode: null,
  resolutionSummaryEnglish: '',
  requestedRealButEffectiveSimulated: false,
  requestedFakeButEffectivePgDump: false,
  fallbackBehavior: 'none',
};

export interface BackupExecutionModeTruth {
  /** GET execution-mode yanıtı mevcut mu */
  loaded: boolean;
  /** İstenen (kalıcı) kullanıcı modu: UseConfigurationDefault | Fake | RealPgDump */
  requestedUserFacingMode: string;
  /** Çözümlenmiş etkin kullanıcı modu (Fake | RealPgDump | ProductionStub) */
  effectiveUserFacingMode: string;
  /** Yapılandırma dosyası varsayılanının kullanıcı modu karşılığı */
  configurationDefaultUserFacingMode: string;
  effectiveExecutionAdapterKind: string;
  configurationExecutionAdapterKind: string;
  /** Etkin çalıştırma Fake veya ProductionStub */
  effectiveIsSimulatedAdapter: boolean;
  /** Etkin çalıştırma PgDump hedefi */
  effectiveIsPgDumpAdapter: boolean;
  /** Sunucu: bu profil şu an sağlıklı çalıştırılabilir mi */
  effectiveModeRunnable: boolean;
  /** Real istenmiş, adaptör PgDump ama yapılandırma Unhealthy — sessiz Fake düşüşü yok */
  requestedRealButBlocked: boolean;
  /** Sunucunun önerdiği mod; otomatik uygulanmaz */
  recommendedFallbackUserFacingMode: string | null;
  resolutionSummaryEnglish: string;
  /** İstenen Real iken etkin yüzey simüle — tutarsızlık uyarısı (kısa süreli / hata sinyali) */
  requestedRealButEffectiveSimulated: boolean;
  /** İstenen Fake iken etkin PgDump — tutarsızlık (inherit karışıklığı vb.) */
  requestedFakeButEffectivePgDump: boolean;
  /** Otomatik mod düşürme yok; yalnızca operatör yönlendirmesi */
  fallbackBehavior: 'none' | 'operator_guidance_only';
}

export function isRealRequestedNonRunnableState(d: BackupExecutionModeResponseDto): boolean {
  return (
    d.requestedUserFacingMode === 'RealPgDump' &&
    (d.effectiveExecutionAdapterKind ?? '').trim() === 'PgDump' &&
    d.effectiveModeRunnable === false
  );
}

export function deriveBackupExecutionModeTruth(
  dto: BackupExecutionModeResponseDto | null | undefined
): BackupExecutionModeTruth {
  if (dto == null) return { ...unloadedBackupExecutionModeTruth };

  const effKind = (dto.effectiveExecutionAdapterKind ?? '').trim();
  const effSim = isSimulatedBackupAdapterKind(effKind);
  const effPg = effKind === 'PgDump';
  const req = (dto.requestedUserFacingMode ?? '').trim();
  const effUf = (dto.effectiveUserFacingMode ?? '').trim();

  const requestedRealButEffectiveSimulated = req === 'RealPgDump' && effSim;
  const requestedFakeButEffectivePgDump = req === 'Fake' && effPg;
  const rec = dto.recommendedFallbackUserFacingMode?.trim();

  return {
    loaded: true,
    requestedUserFacingMode: req,
    effectiveUserFacingMode: effUf,
    configurationDefaultUserFacingMode: (dto.configurationDefaultUserFacingMode ?? '').trim(),
    effectiveExecutionAdapterKind: effKind,
    configurationExecutionAdapterKind: (dto.configurationExecutionAdapterKind ?? '').trim(),
    effectiveIsSimulatedAdapter: effSim,
    effectiveIsPgDumpAdapter: effPg,
    effectiveModeRunnable: dto.effectiveModeRunnable === true,
    requestedRealButBlocked: isRealRequestedNonRunnableState(dto),
    recommendedFallbackUserFacingMode: rec && rec.length > 0 ? rec : null,
    resolutionSummaryEnglish: (dto.effectiveModeResolutionSummaryEnglish ?? '').trim(),
    requestedRealButEffectiveSimulated,
    requestedFakeButEffectivePgDump,
    fallbackBehavior: rec && rec.length > 0 ? 'operator_guidance_only' : 'none',
  };
}
