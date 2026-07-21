/**
 * backupDr kritik uyarı metinleri — anlamsal eşlik (EN / DE / TR)
 *
 * Amaç: Yüksek riskli operatör güvenliği ifadelerinin çevirilerde “daha iyimser”
 * anlama kaymasını yakalamak; tam cümle anlık görüntüsü veya kırılgan kelime kelime
 * eşleşme kullanmıyoruz.
 *
 * Karşılaştırma stratejisi:
 * - Her kritik anahtar için yerel dilde “niyet” desenleri (regex listesi, OR): en az biri eşleşmeli.
 * - Desenler olumsuzlama, kapsam sınırı, “kanıt değil”, “API sınırları”, “metadata” vb. taşımalı.
 * - EN metni değişince ilgili EN desenini güncelleyin; DE/TR çevirisi gevşerse test kırılır.
 *
 * Anahtar grupları (family): tam kurtarma kanıtı yok, çıkarım yapma, simüle/stub,
 * güçlü sinyal ama sınırlı, uçtan uca DR yok, harici/metadata, son istek vs son bilinen iyi,
 * restore kanıtı değil, otomatik restore “testli değil”.
 *
 * Kapsam hedefleri (operatör kavramı → test satırları):
 * | Kavram | Örnek family / anahtarlar |
 * |--------|---------------------------|
 * | Kanıt değil / sınırlı kanıt | not_full_recovery_proof, backup_health_not_recovery_proof, progress_ok_not_dr_proof |
 * | Restore kanıtı değil | artifact_verification_not_full_recoverability, restore_verification_stronger_still_not_dr_program, restore_drill_detail_not_full_recovery_proof |
 * | Uçtan uca DR değil | not_end_to_end_dr, restore_readiness_not_e2e_dr |
 * | Harici lifecycle yalnız metadata | external_lifecycle_metadata_not_offsite_proof, external_archive_metadata_scope |
 * | Güçlü sinyaller sınırlı | strong_signals_within_api, strong_signals_hedge_policy, evidence_strong_within_api_limits |
 * | Simüle/Fake/Stub sınırları | simulated_stub_not_production, backup_stub_not_restorable, last_good_simulated_not_dr, fake_mode_not_pg_dump_success |
 * | Son başarı ≠ son bilinen iyi | latest_request_not_lkg_proof, latest_run_distinct_from_recoverability, download_scope_latest_not_auto_lkg |
 * | Bağımsız doğrulanmış hazırlık değil | readiness_backend_not_independent_verified |
 * | Gerçek yol olsa bile tatbikat kanıtsız | real_pg_operational_unproven_until_drill |
 * | Drill geçmişi metadata tam kurtarma değil | restore_history_metadata_not_guarantee |
 */
import fs from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

type LocaleName = 'en' | 'de' | 'tr';

function readLocale(locale: LocaleName): unknown {
  const file = path.join(process.cwd(), 'src', 'i18n', 'locales', locale, 'backupDr.json');
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

function getByPath(root: unknown, dottedPath: string): string | undefined {
  const parts = dottedPath.split('.');
  let cur: unknown = root;
  for (const p of parts) {
    if (cur == null || typeof cur !== 'object' || !(p in (cur as Record<string, unknown>))) {
      return undefined;
    }
    cur = (cur as Record<string, unknown>)[p];
  }
  return typeof cur === 'string' ? cur : undefined;
}

/** Tek satır: aynı güvenlik niyetini üç dilde koruyan minimum desenler (her dilde OR). */
type CriticalIntentRow = {
  family: string;
  key: string;
  note: string;
  patterns: Record<LocaleName, RegExp[]>;
};

const CRITICAL_INTENT_ROWS: CriticalIntentRow[] = [
  {
    family: 'not_full_recovery_proof',
    key: 'recoverability.title',
    note: 'Tam kurtarma kanıtı değil — başlık sınırı',
    patterns: {
      en: [/not full recovery proof/i],
      de: [/kein vollständiger Recovery-Nachweis|kein voll/i],
      tr: [/tam kurtarma kanıtı değil/i],
    },
  },
  {
    family: 'silence_not_infer',
    key: 'operatorTruth.recoverabilityProofGap',
    note: 'Sessizlikten çıkarım yok',
    patterns: {
      en: [/do not infer|silence/i],
      de: [/keine Wiederherstellbarkeit ableiten|Stille/i],
      tr: [/çıkarmayın|sessizlik/i],
    },
  },
  {
    family: 'simulated_stub_not_production',
    key: 'recoverability.simulatedEnvironmentStrip',
    note: 'Fake/stub kanıt zaman damgaları — üretim/gerçek döküm değil',
    patterns: {
      en: [/not.*production recovery|not a real dump|Fake|Stub|bookkeeping/i],
      de: [/kein Produktions-Recovery|kein echter Dump|Fake|Stub|Buchungen/i],
      tr: [/üretim kurtarması.*değil|gerçek döküm değil|Fake|Stub|defter/i],
    },
  },
  {
    family: 'backup_stub_not_restorable',
    key: 'recoverability.proofBlock.backupStub',
    note: 'Simüle yedek hattı — geri yüklenebilir üretim verisi değil',
    patterns: {
      en: [/simulated|stub|not restorable/i],
      de: [/simuliert|Stub|keine wiederherstellbaren/i],
      tr: [/simüle|stub|geri yüklenebilir.*değil|üretim verisi değil/i],
    },
  },
  {
    family: 'latest_request_not_lkg_proof',
    key: 'recoverability.latestRequestVsProofHint',
    note: 'İstek durumu, son bilinen iyi kanıtı değil',
    patterns: {
      en: [/request status only|not.*last known good/i],
      de: [/nur Anforderungsstatus|kein „letzter bekannter guter“/i],
      tr: [/yalnızca istek|kanıtı değil|son bilinen iyi/i],
    },
  },
  {
    family: 'failed_drill_vs_old_proof',
    key: 'recoverability.latestDrillFailedVsProofTimestamps',
    note: 'Eski başarı zamanı güncel hatayı geçersiz kılmaz',
    patterns: {
      en: [/failed|does not override|older/i],
      de: [/fehlgeschlagen|ersetzt die aktuelle|älteren/i],
      tr: [/başarısız|geçersiz|daha eski/i],
    },
  },
  {
    family: 'last_good_simulated_not_dr',
    key: 'recoverability.lastGoodBackupSimulatedWarning',
    note: 'Simüle başarı — üretim pg_dump / DR hazırlığı değil',
    patterns: {
      en: [/simulated|Fake|Stub|not production|not DR readiness/i],
      de: [/simuliert|Fake|Stub|kein Produktions|keine DR-Bereitschaft/i],
      tr: [/simüle|Fake|Stub|üretim.*kanıtı değil|DR hazırlığı değil/i],
    },
  },
  {
    family: 'latest_drill_failed_risk',
    key: 'operatorValidity.latestDrillFailedTitle',
    note: 'Son tatbikat başarısız — risk',
    patterns: {
      en: [/failed|at risk|recoverability/i],
      de: [/fehlgeschlagen|risikobehaftet|Recoverability/i],
      tr: [/başarısız|riskli|kurtarılabilirlik/i],
    },
  },
  {
    family: 'latest_drill_failed_no_strong_posture',
    key: 'operatorValidity.latestDrillFailedBody',
    note: 'Güçlü yedek durumu sayma — önce incele',
    patterns: {
      en: [/did not succeed|not.*strong|Investigate|rerun/i],
      de: [/nicht erfolgreich|nicht.*stark|Analyse|erneut/i],
      tr: [/başarısız|güçlü.*yorumlam|inceleyip|yeniden/i],
    },
  },
  {
    family: 'strong_signals_within_api',
    key: 'operatorValidity.strongSignalsTitle',
    note: 'En iyi sinyal ifadesi ama API çerçevesi / sınırlar (güçlü kelimesi zorunlu değil)',
    patterns: {
      en: [/best available|within API|limits|Recoverability|signals/i],
      de: [/am besten verfügbar|API-Rahmen|Recoverability/i],
      tr: [/eldeki en iyi|API sınırları|kurtarılabilirlik/i],
    },
  },
  {
    family: 'strong_signals_hedge_policy',
    key: 'operatorValidity.strongSignalsBody',
    note: 'En iyi sinyal + politika dışı doğrulama',
    patterns: {
      en: [/best available|Still validate|outside the UI|policy|RPO/i],
      de: [/bestes verfügbares|weiterhin|policy|RPO|prüfen/i],
      tr: [/en iyi sinyal|yine de|politika|RPO|doğrulayın/i],
    },
  },
  {
    family: 'not_end_to_end_dr',
    key: 'readiness.notEndToEndDr',
    note: 'Tam üretim uçtan uca kurtarma kanıtı değil',
    patterns: {
      en: [/does not prove|end-to-end|full production/i],
      de: [
        /beweist nicht|Ende-zu-Ende|vollständige Produktion|kein vollständiger Produktions-Recovery|Nachweis/i,
      ],
      tr: [/kanıtlamaz|uçtan uca|tam üretim/i],
    },
  },
  {
    family: 'backup_health_not_recovery_proof',
    key: 'summary.backupHealthFootnote',
    note: 'Yapılandırma sinyali — kurtarma çalışacağı kanıtı değil',
    patterns: {
      en: [/not proof|recovery will work/i],
      de: [/kein Nachweis|Recovery wird funktionieren/i],
      tr: [/kanıtı değil|kurtarma.*çalışacağı/i],
    },
  },
  {
    family: 'restore_readiness_not_e2e_dr',
    key: 'summary.restoreReadinessFootnote',
    note: 'Kanıtlanmış uçtan uca DR değil',
    patterns: {
      en: [/not proven|end-to-end DR/i],
      de: [/kein nachgewiesenes|Ende-zu-Ende-DR/i],
      tr: [/kanıtı değil|uçtan uca DR/i],
    },
  },
  {
    family: 'external_archive_metadata_scope',
    key: 'summary.externalArchiveCard',
    note: 'Harici arşiv — metadata, son çalıştırma',
    patterns: {
      en: [/metadata|latest run/i],
      de: [/Metadaten|letzten Lauf/i],
      tr: [/metadata|son çalıştırma/i],
    },
  },
  {
    family: 'external_lifecycle_metadata_not_offsite_proof',
    key: 'externalCopy.externalLifecycleOk',
    note: 'Yalnızca metadata — bağımsız off-site kanıt değil',
    patterns: {
      en: [/Metadata only|not independent|recovery proof/i],
      de: [/Nur Metadaten|nicht unabhängig|Recovery-Nachweis/i],
      tr: [/yalnızca metadata|bağımsız değil|kanıt/i],
    },
  },
  {
    family: 'lifecycle_scope_not_recoverability_card',
    key: 'externalCopy.scopeFromLatestRun',
    note: 'Kurtarılabilirlik özet kartından değil',
    patterns: {
      en: [/not the recoverability|latest backup run detail/i],
      de: [/nicht von der Recoverability|letzten Backup-Lauf/i],
      tr: [/kurtarılabilirlik.*değil|son yedek.*detay/i],
    },
  },
  {
    family: 'readiness_backend_not_independent_verified',
    key: 'readiness.backendReportedSignal',
    note: 'Sunucu sinyali — bağımsız doğrulanmış operasyonel hazırlık değil',
    patterns: {
      en: [/not independently verified|operational readiness/i],
      de: [/keine unabhängig|unabhängig verifiziert|Betriebsbereitschaft/i],
      tr: [/bağımsız doğrulanmış operasyonel hazırlık değildir|bağımsız/i],
    },
  },
  {
    family: 'real_pg_operational_unproven_until_drill',
    key: 'operatorValidity.realPgOperationalBody',
    note: 'Gerçek pg_dump bildirilse bile geçen tatbikat olana kadar kurtarma kanıtsız',
    patterns: {
      en: [/unproven|until a passing drill|policy/i],
      de: [/unbelegt|passenden Drill|Policy|nicht erfolgreich/i],
      tr: [/kanıtsız|geçen bir tatbikat|politika|başarılı değil/i],
    },
  },
  {
    family: 'restore_history_metadata_not_guarantee',
    key: 'restoreHistory.statusHint',
    note: 'Drill satırları teknik metadata — tam kurtarma garantisi değil',
    patterns: {
      en: [/technical metadata|not a full recovery guarantee/i],
      de: [/technische Metadaten|keine vollständige Recovery-Garantie/i],
      tr: [/teknik metadata|tam kurtarma garantisi değildir|metadata/i],
    },
  },
  {
    family: 'progress_ok_not_dr_proof',
    key: 'progress.finishedOk',
    note: 'Operasyonel tamam — DR kanıtı değil',
    patterns: {
      en: [/Operational only|Not DR proof|non-simulated|API reports success/i],
      de: [/Nur operativ|Kein DR|nicht simulierter/i],
      tr: [/Yalnızca operasyonel|DR kanıtı|simüle olmayan/i],
    },
  },
  {
    family: 'progress_success_drill_failed_glance',
    key: 'progress.finishedOkLatestDrillFailed',
    note: 'Yedek başarılı + son tatbikat başarısız — telafi yok',
    patterns: {
      en: [/restore drill failed|see restore verification|Latest backup run succeeded/i],
      de: [/Restore-Drill|fehlgeschlagen|Restore-Verifikation/i],
      tr: [/restore tatbikatı başarısız|Geri yükleme doğrulaması/i],
    },
  },
  {
    family: 'progress_success_drill_failed_detail',
    key: 'progress.finishedOkLatestDrillFailedDetail',
    note: 'Teknik yedek başarısı tatbikatı geçersiz kılmaz',
    patterns: {
      en: [/does not override|failed restore drill|recoverability|policy/i],
      de: [/ersetzt keinen|fehlgeschlagenen Restore-Drill|Recoverability|Policy/i],
      tr: [/geçersiz kılmaz|başarısız restore|kurtarılabilirlik|politika/i],
    },
  },
  {
    family: 'progress_unproven_proof_gap',
    key: 'progress.finishedOkUnproven',
    note: 'Kanıt eksik veya eski',
    patterns: {
      en: [/incomplete|stale|proof/i],
      de: [/unvollständig|veraltet|Nachweis/i],
      tr: [/eksik|eski|kanıt/i],
    },
  },
  {
    family: 'progress_unproven_no_dr_until_aligned',
    key: 'progress.finishedOkUnprovenDetail',
    note: 'DR hazır sanma — tatbikat ve kanıt hizalanana kadar',
    patterns: {
      en: [/operational completion only|Do not infer DR readiness|aligned/i],
      de: [/operativen Abschluss|DR-Bereitschaft|zusammenpassen/i],
      tr: [/operasyonel|DR hazır|hizalanmadan/i],
    },
  },
  {
    family: 'evidence_drill_failed_not_current_confidence',
    key: 'evidence.headline.latestDrillFailed',
    note: 'Önceki yeşil sinyal güncel güven değil',
    patterns: {
      en: [/failed|do not treat|confidence|green/i],
      de: [/fehlgeschlagen|nicht.*lesen|grün|Sicherheit/i],
      tr: [/başarısız|okumayın|yeşil|güvence/i],
    },
  },
  {
    family: 'evidence_strong_within_api_limits',
    key: 'evidence.headline.strongWithinApi',
    note: 'API sınırları içinde güçlü — politika dışında',
    patterns: {
      en: [/within API limits|still validate|outside|policy/i],
      de: [/innerhalb der API-Grenzen|extern|Policy/i],
      tr: [/API sınırları|dışında|politika/i],
    },
  },
  {
    family: 'evidence_stub_no_restorable_dump',
    key: 'evidence.headline.stubPipeline',
    note: 'Stub hat — geri yüklenebilir arşiv üretmez',
    patterns: {
      en: [/does not produce|restorable|PostgreSQL/i],
      de: [/liefert kein|wiederherstellbares|PostgreSQL/i],
      tr: [/üretmez|geri yüklenebilir|PostgreSQL/i],
    },
  },
  {
    family: 'restore_drill_detail_not_full_recovery_proof',
    key: 'evidence.steps.restoreDrillCompleted.detailPass',
    note: 'Tatbikat başarılı — tam üretim kurtarma kanıtı değil',
    patterns: {
      en: [/still not|full production recovery proof/i],
      de: [/kein vollständiges Recovery-Programm|kein vollständiger/i],
      tr: [/tam.*kurtarma programı değil|tam üretim.*değil/i],
    },
  },
  {
    family: 'artifact_verification_not_full_recoverability',
    key: 'artifactVerification.notRestoreProof',
    note: 'Bütünlük — tam kurtarılabilirlik değil',
    patterns: {
      en: [/not full recoverability/i],
      de: [/keine vollständige Wiederherstellbarkeit|nicht vollständige/i],
      tr: [/tam kurtarılabilirlik değil/i],
    },
  },
  {
    family: 'restore_verification_stronger_still_not_dr_program',
    key: 'restoreVerification.strongerThanArtifact',
    note: 'Daha fazla teknik kontrol — yine tam DR programı / üretim kanıtı değil',
    patterns: {
      en: [/still not|full disaster recovery|proof of production recovery/i],
      de: [/dennoch kein vollständiges Disaster-Recovery|kein Produktions-Recovery-Nachweis/i],
      tr: [/yine de|felaket kurtarma programı|üretim kurtarma kanıtı değil/i],
    },
  },
  {
    family: 'distinction_not_full_restore_proof',
    key: 'distinction.body',
    note: 'Tam geri yükleme kanıtı ve RPO/RTO garantisi değil',
    patterns: {
      en: [/not a full restore proof|does not guarantee|RPO/i],
      de: [/ersetzt weder|keine.*Garantien|RPO/i],
      tr: [/tam geri yükleme kanıtı|garantisi değil|RPO/i],
    },
  },
  {
    family: 'restore_capability_not_tested_restore',
    key: 'restoreCapability.reportedEnabled',
    note: 'Backend alanı — testli geri yükleme değil',
    patterns: {
      en: [/not a tested restore/i],
      de: [/kein getesteter Restore/i],
      tr: [/testli geri yükleme değil/i],
    },
  },
  {
    family: 'latest_run_distinct_from_recoverability',
    key: 'latestRun.distinctFromRecoverability',
    note: 'Son çalıştırma, üstteki son bilinen iyi kanıtla aynı değil',
    patterns: {
      en: [/not the same|last known good/i],
      de: [/nicht dasselbe|letzter bekannter guter/i],
      tr: [/aynı değil|son bilinen iyi/i],
    },
  },
  {
    family: 'download_scope_latest_not_auto_lkg',
    key: 'download.scopeLatestSuccess',
    note: 'Son başarılı çalıştırma — recoverability kartı ile otomatik aynı değil',
    patterns: {
      en: [/not recoverability proof|recoverability card/i],
      de: [/nicht.*derselbe|Recoverability-Karte|Nachweis/i],
      tr: [/aynı değil|kurtarılabilirlik/i],
    },
  },
  {
    family: 'download_scope_lkg_not_dr_guarantee',
    key: 'download.scopeLastKnownGood',
    note: 'DR garantisi değil — operasyonel dosya listesi',
    patterns: {
      en: [/not a DR guarantee|operational file list/i],
      de: [/Kein DR-Garantie|operativen/i],
      tr: [/DR garantisi değil|operasyonel/i],
    },
  },
  {
    family: 'fake_mode_not_pg_dump_success',
    key: 'fakeMode.bannerBody',
    note: 'Fake — pg_dump yok; başarı orkestrasyon; arşiv değil',
    patterns: {
      en: [/no pg_dump|not a restorable|integration testing|bookkeeping|placeholders/i],
      de: [/kein pg_dump|kein wiederherstellbares|Integrationstests|Buchführung|Platzhalter/i],
      tr: [/pg_dump çalıştırmaz|geri yüklenebilir.*değil|entegrasyon|kayıt|yer tutucu/i],
    },
  },
];

describe('backupDr critical warning copy parity', () => {
  const locales: Record<LocaleName, unknown> = {
    en: readLocale('en'),
    de: readLocale('de'),
    tr: readLocale('tr'),
  };

  it('CRITICAL_INTENT_ROWS anahtarları tekil (aynı key iki kez tanımlanmasın)', () => {
    const keys = CRITICAL_INTENT_ROWS.map((r) => r.key);
    expect(
      new Set(keys).size,
      `yinelenen key: ${keys.filter((k, i) => keys.indexOf(k) !== i).join(', ')}`
    ).toBe(keys.length);
  });

  it('tüm kritik anahtarlar en/de/tr içinde dolu', () => {
    for (const row of CRITICAL_INTENT_ROWS) {
      for (const locale of ['en', 'de', 'tr'] as const) {
        const value = getByPath(locales[locale], row.key);
        expect(value, `${locale} eksik: ${row.key}`).toBeTruthy();
        expect(String(value).trim().length, `${locale} boş: ${row.key}`).toBeGreaterThan(0);
      }
    }
  });

  it('kritik niyet desenleri (anlamsal) üç dilde korunuyor', () => {
    for (const row of CRITICAL_INTENT_ROWS) {
      for (const locale of ['en', 'de', 'tr'] as const) {
        const raw = getByPath(locales[locale], row.key);
        expect(raw, `${locale} ${row.key}`).toBeTruthy();
        const value = raw as string;
        const patterns = row.patterns[locale];
        const ok = patterns.some((re) => re.test(value));
        expect(
          ok,
          `[${row.family}] ${locale} ${row.key}: niyet eşleşmedi. Not: ${row.note}\nMetin: ${value.slice(0, 280)}`
        ).toBe(true);
      }
    }
  });

  it('kritik metinlerde aşırı iyimserlik ifadeleri (blok listesi) yok', () => {
    const forbidden = [
      /\bguaranteed\s+recovery\b/i,
      /\bfull\s+DR\s+guaranteed\b/i,
      /\b100%\s+recoverable\b/i,
      /\bcompletely\s+safe\s+recovery\b/i,
    ];
    for (const row of CRITICAL_INTENT_ROWS) {
      for (const locale of ['en', 'de', 'tr'] as const) {
        const raw = getByPath(locales[locale], row.key);
        expect(raw).toBeTruthy();
        const value = raw as string;
        for (const re of forbidden) {
          expect(re.test(value), `[${row.key}] ${locale}: yasak iyimser ifade: ${re}`).toBe(false);
        }
      }
    }
  });
});
