/**
 * Kurtarılabilirlik kartı: kanıt tazeliği ve operatör için yaş metni (yeşil = “her şey iyi” ima etmez).
 */

export type ProofAgeFreshnessTier = 'unknown' | 'recent' | 'aging' | 'stale';

const SEC_DAY = 86400;
/** 24 saat: “aging” eşiği — RPO uyarısı için. */
const AGING_THRESHOLD_SEC = SEC_DAY;
/** 7 gün: “stale” — kanıt eskisi. */
const STALE_THRESHOLD_SEC = 7 * SEC_DAY;

export function proofAgeFreshnessTier(ageSeconds: number | null | undefined): ProofAgeFreshnessTier {
  if (ageSeconds === null || ageSeconds === undefined || Number.isNaN(ageSeconds)) return 'unknown';
  if (ageSeconds >= STALE_THRESHOLD_SEC) return 'stale';
  if (ageSeconds >= AGING_THRESHOLD_SEC) return 'aging';
  return 'recent';
}

/** i18n: recoverability.freshnessTag.* */
export function freshnessTagColor(
  tier: ProofAgeFreshnessTier,
): 'blue' | 'orange' | 'red' | 'default' {
  if (tier === 'recent') return 'blue';
  if (tier === 'aging') return 'orange';
  if (tier === 'stale') return 'red';
  return 'default';
}
