/**
 * RKSV hub — tek kaynak: NEXT_PUBLIC_RKSV_ENVIRONMENT (Next.js build-time public env).
 * Şu an yalnızca TEST ve PROD kabul edilir; genişletme için RKSV_ENV_ACCEPTED_PUBLIC kullanılır.
 * Türkçe: process.env doğrudan başka yerde okunmaz; strict state makinesi.
 * Üretim derlemesi: `next.config.mjs` aynı değişkeni `next build` öncesi doğrular (sessiz artefakt önlenir).
 */

/** Next.js public env anahtarı — projede yalnızca bu modül okur. */
export const RKSV_PUBLIC_ENV_VAR_NAME = 'NEXT_PUBLIC_RKSV_ENVIRONMENT' as const;

export const RKSV_ENV_ACCEPTED_PUBLIC = ['TEST', 'PROD'] as const;

export type RksvEnvCanonical = (typeof RKSV_ENV_ACCEPTED_PUBLIC)[number];

/** Strict parser çıktısı — UI ve loglar bu dört duruma göre ayrılır. */
export const RksvPublicEnvironmentState = {
  TEST: 'TEST',
  PROD: 'PROD',
  UNCONFIGURED: 'UNCONFIGURED',
  INVALID: 'INVALID',
} as const;

export type RksvPublicEnvironmentState =
  (typeof RksvPublicEnvironmentState)[keyof typeof RksvPublicEnvironmentState];

export type StrictParsedRksvPublicEnvironment =
  | { state: typeof RksvPublicEnvironmentState.TEST }
  | { state: typeof RksvPublicEnvironmentState.PROD }
  | { state: typeof RksvPublicEnvironmentState.UNCONFIGURED }
  | { state: typeof RksvPublicEnvironmentState.INVALID; sanitizedRaw: string };

/** Ant Design Alert: TEST uyarı (non-prod), PROD/INVALID error, UNCONFIGURED uyarı. */
export type RksvEnvironmentAlertType = 'info' | 'warning' | 'error';

export type RksvEnvironmentBadgeColor = 'warning' | 'error' | 'processing';

const I18N_PREFIX = 'rksvHub.env' as const;

export const RKSV_ENV_I18N_KEYS = {
  displayLabel: {
    test: `${I18N_PREFIX}.displayLabel.test`,
    prod: `${I18N_PREFIX}.displayLabel.prod`,
    unconfigured: `${I18N_PREFIX}.displayLabel.unconfigured`,
    invalid: `${I18N_PREFIX}.displayLabel.invalid`,
  },
  banner: {
    test: `${I18N_PREFIX}.banner.test`,
    prod: `${I18N_PREFIX}.banner.prod`,
    unconfigured: `${I18N_PREFIX}.banner.unconfigured`,
    invalid: `${I18N_PREFIX}.banner.invalid`,
  },
  hint: {
    test: `${I18N_PREFIX}.hint.test`,
    prod: `${I18N_PREFIX}.hint.prod`,
    unconfigured: `${I18N_PREFIX}.hint.unconfigured`,
    invalid: `${I18N_PREFIX}.hint.invalid`,
  },
} as const;

export type RksvEnvironmentDisplayLabelKey =
  (typeof RKSV_ENV_I18N_KEYS)['displayLabel'][keyof (typeof RKSV_ENV_I18N_KEYS)['displayLabel']];

export type RksvEnvironmentBannerMessageKey =
  (typeof RKSV_ENV_I18N_KEYS)['banner'][keyof (typeof RKSV_ENV_I18N_KEYS)['banner']];

export type RksvEnvironmentHintKey =
  (typeof RKSV_ENV_I18N_KEYS)['hint'][keyof (typeof RKSV_ENV_I18N_KEYS)['hint']];

const MAX_RAW_DISPLAY_LEN = 96;

/** UTF-8 BOM + trim; bazı editörler .env başına FEFF ekler — yalnızca trim() yetmez. */
function stripBomAndTrim(s: string): string {
  return s.replace(/^\uFEFF/, '').trim();
}

/**
 * Geliştirme teşhisi: parse öncesi ham değer + anahtarın process.env’de görünürlüğü.
 * Türkçe: Üretimde kullanılmaz; kaldırmak için `buildRksvEnvironmentDevParseDebug` + hook UI satırını silin.
 */
export type RksvEnvironmentDevParseDebug = {
  envVarName: typeof RKSV_PUBLIC_ENV_VAR_NAME;
  /** `process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT !== undefined` (boş string yine “tanımlı”). */
  processEnvKeyPresent: boolean;
  raw: string;
  rawJson: string;
  trimmed: string;
  normalizedUpper: string;
  parsedState: RksvPublicEnvironmentState;
  /** Bundle’da değerin nereden geldiği (şimdilik sabit). */
  source: 'process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT';
};

export function isRksvEnvironmentDevDebugEnabled(): boolean {
  return typeof process !== 'undefined' && process.env.NODE_ENV === 'development';
}

/**
 * Next.js client tarafında `NEXT_PUBLIC_*` genelde yalnızca doğrudan property erişiminde inline edilir.
 * Bu yüzden burada literal kullanılıyor; `RKSV_PUBLIC_ENV_VAR_NAME` ile aynı olmalı.
 * Türkçe: Dinamik `process.env[key]` bazen bundle’da undefined kalır → yanlış UNCONFIGURED.
 */
function readNextPublicRksvEnvironmentValue(): string | undefined {
  if (typeof process === 'undefined') return undefined;
  return process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT;
}

function isRksvEnvCanonical(value: string): value is RksvEnvCanonical {
  return (RKSV_ENV_ACCEPTED_PUBLIC as readonly string[]).includes(value);
}

/**
 * Ham env değerini UI’da göstermeden önce sınırlar (kontrol karakterleri, uzunluk).
 * Türkçe: XSS için metin düğümü yeterli; yine de operatör konsolunu kirletmemek için sadeleştirir.
 */
export function sanitizeRksvEnvironmentRawForDisplay(raw: string): string {
  const trimmed = stripBomAndTrim(raw);
  const withoutControls = trimmed.replace(/[\u0000-\u001F\u007F-\u009F]/g, '\uFFFD');
  if (withoutControls.length <= MAX_RAW_DISPLAY_LEN) return withoutControls;
  return `${withoutControls.slice(0, MAX_RAW_DISPLAY_LEN)}…`;
}

/** Tek nokta: process.env okuma (testlerde stub’lanabilir). */
export function readRawRksvPublicEnvironmentFromProcess(): string {
  const v = readNextPublicRksvEnvironmentValue();
  if (v === undefined || v === null) return '';
  return String(v);
}

/** Dev-only: UNCONFIGURED teşhisi için anahtar var mı / ham string. */
export function getRksvPublicEnvRawMeta(): { processEnvKeyPresent: boolean; raw: string } {
  const v = readNextPublicRksvEnvironmentValue();
  const processEnvKeyPresent = v !== undefined;
  const raw = v === undefined || v === null ? '' : String(v);
  return { processEnvKeyPresent, raw };
}

/**
 * Yalnızca `NODE_ENV === 'development'` iken anlamlı; aksi halde `null` döner.
 * Türkçe: Geçici teşhis — üretim bundle’ına küçük dead branch olarak kalabilir; tam temizlik için kaldırın.
 */
export function buildRksvEnvironmentDevParseDebug(): RksvEnvironmentDevParseDebug | null {
  if (!isRksvEnvironmentDevDebugEnabled()) return null;
  const meta = getRksvPublicEnvRawMeta();
  const parsed = parseStrictRksvPublicEnvironment(meta.raw);
  const trimmed = stripBomAndTrim(meta.raw);
  return {
    envVarName: RKSV_PUBLIC_ENV_VAR_NAME,
    processEnvKeyPresent: meta.processEnvKeyPresent,
    raw: meta.raw,
    rawJson: JSON.stringify(meta.raw),
    trimmed,
    normalizedUpper: trimmed.toUpperCase(),
    parsedState: parsed.state,
    source: 'process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT',
  };
}

export function logRksvEnvironmentDevParseDebug(debug: RksvEnvironmentDevParseDebug | null): void {
  if (!debug || typeof console === 'undefined') return;
  console.info('[RKSV env dev debug]', debug);
}

/**
 * Strict parse: TEST | PROD | UNCONFIGURED | INVALID.
 * Boş/ yalnızca boşluk → UNCONFIGURED; bilinen dışı → INVALID (sanitizedRaw ile).
 */
export function parseStrictRksvPublicEnvironment(
  rawFromEnv: string = readRawRksvPublicEnvironmentFromProcess()
): StrictParsedRksvPublicEnvironment {
  const trimmed = stripBomAndTrim(rawFromEnv);
  if (trimmed === '') {
    return { state: RksvPublicEnvironmentState.UNCONFIGURED };
  }
  const normalized = trimmed.toUpperCase();
  if (isRksvEnvCanonical(normalized)) {
    return normalized === 'TEST'
      ? { state: RksvPublicEnvironmentState.TEST }
      : { state: RksvPublicEnvironmentState.PROD };
  }
  return {
    state: RksvPublicEnvironmentState.INVALID,
    sanitizedRaw: sanitizeRksvEnvironmentRawForDisplay(rawFromEnv),
  };
}

/** Geriye dönük isim — aynı strict sonuç. */
export function parseRksvPublicEnvironment(): StrictParsedRksvPublicEnvironment {
  return parseStrictRksvPublicEnvironment();
}

export type RksvPublicEnvironmentIssue =
  | { code: 'missing' }
  | { code: 'invalid'; raw: string };

export function isRksvPublicEnvironmentConfigured(
  parsed: StrictParsedRksvPublicEnvironment
): parsed is { state: typeof RksvPublicEnvironmentState.TEST } | { state: typeof RksvPublicEnvironmentState.PROD } {
  return parsed.state === RksvPublicEnvironmentState.TEST || parsed.state === RksvPublicEnvironmentState.PROD;
}

export function getRksvPublicEnvironmentIssue(parsed: StrictParsedRksvPublicEnvironment): RksvPublicEnvironmentIssue | null {
  if (parsed.state === RksvPublicEnvironmentState.UNCONFIGURED) return { code: 'missing' };
  if (parsed.state === RksvPublicEnvironmentState.INVALID) return { code: 'invalid', raw: parsed.sanitizedRaw };
  return null;
}

let rksvPublicEnvConsoleWarnEmitted = false;

export function warnRksvPublicEnvironmentInConsole(parsed: StrictParsedRksvPublicEnvironment): void {
  if (typeof console === 'undefined' || rksvPublicEnvConsoleWarnEmitted) return;
  if (isRksvPublicEnvironmentConfigured(parsed)) return;
  rksvPublicEnvConsoleWarnEmitted = true;
  const allowed = RKSV_ENV_ACCEPTED_PUBLIC.join('|');
  const name = RKSV_PUBLIC_ENV_VAR_NAME;
  if (parsed.state === RksvPublicEnvironmentState.UNCONFIGURED) {
    console.warn(
      `[RKSV Admin] ${name} missing in client bundle (compile-time). Fix: frontend-admin/.env.local (same folder as package.json, not monorepo root). Line: NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST or PROD. cd frontend-admin && npm run dev — if still UNCONFIGURED: npm run dev:clean. NEXT_PUBLIC_* is inlined at next dev/build, not at runtime.`
    );
  } else {
    console.warn(
      `[RKSV Admin] ${name} is invalid (display-safe: "${parsed.sanitizedRaw}"). Allowed: ${allowed}. Update .env / CI secrets and rebuild the admin client.`
    );
  }
}

export function getRksvEnvironmentDisplayLabelKey(parsed: StrictParsedRksvPublicEnvironment): RksvEnvironmentDisplayLabelKey {
  switch (parsed.state) {
    case RksvPublicEnvironmentState.TEST:
      return RKSV_ENV_I18N_KEYS.displayLabel.test;
    case RksvPublicEnvironmentState.PROD:
      return RKSV_ENV_I18N_KEYS.displayLabel.prod;
    case RksvPublicEnvironmentState.UNCONFIGURED:
      return RKSV_ENV_I18N_KEYS.displayLabel.unconfigured;
    case RksvPublicEnvironmentState.INVALID:
      return RKSV_ENV_I18N_KEYS.displayLabel.invalid;
  }
}

export function getRksvEnvironmentBannerKeys(parsed: StrictParsedRksvPublicEnvironment): {
  messageKey: RksvEnvironmentBannerMessageKey;
  descriptionKey: RksvEnvironmentHintKey;
  i18nParams?: { envVar: typeof RKSV_PUBLIC_ENV_VAR_NAME; value?: string };
} {
  const envVar = RKSV_PUBLIC_ENV_VAR_NAME;
  switch (parsed.state) {
    case RksvPublicEnvironmentState.TEST:
      return {
        messageKey: RKSV_ENV_I18N_KEYS.banner.test,
        descriptionKey: RKSV_ENV_I18N_KEYS.hint.test,
      };
    case RksvPublicEnvironmentState.PROD:
      return {
        messageKey: RKSV_ENV_I18N_KEYS.banner.prod,
        descriptionKey: RKSV_ENV_I18N_KEYS.hint.prod,
      };
    case RksvPublicEnvironmentState.UNCONFIGURED:
      return {
        messageKey: RKSV_ENV_I18N_KEYS.banner.unconfigured,
        descriptionKey: RKSV_ENV_I18N_KEYS.hint.unconfigured,
        i18nParams: { envVar },
      };
    case RksvPublicEnvironmentState.INVALID:
      return {
        messageKey: RKSV_ENV_I18N_KEYS.banner.invalid,
        descriptionKey: RKSV_ENV_I18N_KEYS.hint.invalid,
        i18nParams: { envVar, value: parsed.sanitizedRaw },
      };
  }
}

export function getRksvEnvironmentAlertType(parsed: StrictParsedRksvPublicEnvironment): RksvEnvironmentAlertType {
  switch (parsed.state) {
    case RksvPublicEnvironmentState.TEST:
      return 'warning';
    case RksvPublicEnvironmentState.PROD:
    case RksvPublicEnvironmentState.INVALID:
      return 'error';
    case RksvPublicEnvironmentState.UNCONFIGURED:
      return 'warning';
  }
}

export function getRksvEnvironmentBadgeColor(parsed: StrictParsedRksvPublicEnvironment): RksvEnvironmentBadgeColor {
  switch (parsed.state) {
    case RksvPublicEnvironmentState.TEST:
      return 'warning';
    case RksvPublicEnvironmentState.PROD:
    case RksvPublicEnvironmentState.INVALID:
      return 'error';
    case RksvPublicEnvironmentState.UNCONFIGURED:
      return 'warning';
  }
}
