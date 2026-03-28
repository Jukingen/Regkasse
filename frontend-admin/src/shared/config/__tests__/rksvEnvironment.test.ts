import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  getRksvEnvironmentAlertType,
  getRksvEnvironmentBadgeColor,
  getRksvEnvironmentDisplayLabelKey,
  parseStrictRksvPublicEnvironment,
  RKSV_ENV_I18N_KEYS,
  RKSV_PUBLIC_ENV_VAR_NAME,
  RksvPublicEnvironmentState,
  sanitizeRksvEnvironmentRawForDisplay,
  type RksvEnvironmentAlertType,
  type RksvEnvironmentBadgeColor,
  type RksvEnvironmentDisplayLabelKey,
  type RksvEnvCanonical,
  type StrictParsedRksvPublicEnvironment,
} from '../rksvEnvironment';

/** Kanonik ortam: yalnızca geçerli TEST/PROD; aksi halde null (INVALID / UNCONFIGURED). */
type NormalizedRksvEnv = RksvEnvCanonical | null;

type RksvParseExpectation = {
  input: string;
  state: RksvPublicEnvironmentState;
  /** Geçerli TEST/PROD için normalize edilmiş kanonik değer; değilse null. */
  normalized: NormalizedRksvEnv;
  /** i18n displayLabel anahtarı (UI etiketi). */
  displayLabelKey: RksvEnvironmentDisplayLabelKey;
  /** Alert ciddiyeti (Ant Design `type`). */
  alertType: RksvEnvironmentAlertType;
  /** Badge rengi (Ant Design Tag `color`). */
  badgeColor: RksvEnvironmentBadgeColor;
  /** INVALID ise beklenen sanitize edilmiş ham değer. */
  sanitizedRaw?: string;
};

function normalizedCanonical(parsed: StrictParsedRksvPublicEnvironment): NormalizedRksvEnv {
  if (parsed.state === RksvPublicEnvironmentState.TEST) return 'TEST';
  if (parsed.state === RksvPublicEnvironmentState.PROD) return 'PROD';
  return null;
}

function assertRksvParse(raw: string, exp: RksvParseExpectation): void {
  const parsed = parseStrictRksvPublicEnvironment(raw);
  expect(parsed.state, `state for input ${JSON.stringify(raw)}`).toBe(exp.state);
  expect(normalizedCanonical(parsed), `normalized for input ${JSON.stringify(raw)}`).toBe(exp.normalized);
  expect(getRksvEnvironmentDisplayLabelKey(parsed), `label key for input ${JSON.stringify(raw)}`).toBe(
    exp.displayLabelKey
  );
  expect(getRksvEnvironmentAlertType(parsed), `alert for input ${JSON.stringify(raw)}`).toBe(exp.alertType);
  expect(getRksvEnvironmentBadgeColor(parsed), `badge for input ${JSON.stringify(raw)}`).toBe(exp.badgeColor);
  if (exp.state === RksvPublicEnvironmentState.INVALID) {
    expect(parsed.state).toBe(RksvPublicEnvironmentState.INVALID);
    if (parsed.state === RksvPublicEnvironmentState.INVALID) {
      expect(parsed.sanitizedRaw).toBe(exp.sanitizedRaw);
    }
  }
}

/** Tablo: istenen girdi → state, normalized, UI label key, severity (alert + badge). */
const PARSE_CASES: RksvParseExpectation[] = [
  {
    input: 'TEST',
    state: RksvPublicEnvironmentState.TEST,
    normalized: 'TEST',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.test,
    alertType: 'warning',
    badgeColor: 'warning',
  },
  {
    input: 'PROD',
    state: RksvPublicEnvironmentState.PROD,
    normalized: 'PROD',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.prod,
    alertType: 'error',
    badgeColor: 'error',
  },
  {
    input: 'test',
    state: RksvPublicEnvironmentState.TEST,
    normalized: 'TEST',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.test,
    alertType: 'warning',
    badgeColor: 'warning',
  },
  {
    input: 'prod',
    state: RksvPublicEnvironmentState.PROD,
    normalized: 'PROD',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.prod,
    alertType: 'error',
    badgeColor: 'error',
  },
  {
    input: ' TEST ',
    state: RksvPublicEnvironmentState.TEST,
    normalized: 'TEST',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.test,
    alertType: 'warning',
    badgeColor: 'warning',
  },
  {
    input: ' prod ',
    state: RksvPublicEnvironmentState.PROD,
    normalized: 'PROD',
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.prod,
    alertType: 'error',
    badgeColor: 'error',
  },
  {
    input: '',
    state: RksvPublicEnvironmentState.UNCONFIGURED,
    normalized: null,
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.unconfigured,
    alertType: 'warning',
    badgeColor: 'warning',
  },
  {
    input: 'abc',
    state: RksvPublicEnvironmentState.INVALID,
    normalized: null,
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.invalid,
    alertType: 'error',
    badgeColor: 'error',
    sanitizedRaw: 'abc',
  },
  {
    input: 'staging',
    state: RksvPublicEnvironmentState.INVALID,
    normalized: null,
    displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.invalid,
    alertType: 'error',
    badgeColor: 'error',
    sanitizedRaw: 'staging',
  },
];

describe('parseStrictRksvPublicEnvironment — tablo (TEST / PROD / boş / geçersiz)', () => {
  it.each(PARSE_CASES)('input $input → $state', (row) => {
    assertRksvParse(row.input, row);
  });
});

/**
 * `undefined` ortam değişkeni: default parametre `readRawRksvPublicEnvironmentFromProcess()` çağrılır;
 * `process.env[NEXT_PUBLIC_RKSV_ENVIRONMENT]` yoksa ham değer '' sayılır → UNCONFIGURED.
 */
describe('parseStrictRksvPublicEnvironment — process.env tanımsız (undefined)', () => {
  const key = RKSV_PUBLIC_ENV_VAR_NAME;
  let previous: string | undefined;

  beforeEach(() => {
    previous = process.env[key];
    delete process.env[key];
  });

  afterEach(() => {
    if (previous === undefined) {
      delete process.env[key];
    } else {
      process.env[key] = previous;
    }
  });

  it('argüman verilmeden çağrı UNCONFIGURED + aynı UI/severity eşlemesi', () => {
    const parsed = parseStrictRksvPublicEnvironment();
    expect(parsed).toEqual({ state: RksvPublicEnvironmentState.UNCONFIGURED });
    expect(normalizedCanonical(parsed)).toBeNull();
    expect(getRksvEnvironmentDisplayLabelKey(parsed)).toBe(RKSV_ENV_I18N_KEYS.displayLabel.unconfigured);
    expect(getRksvEnvironmentAlertType(parsed)).toBe('warning');
    expect(getRksvEnvironmentBadgeColor(parsed)).toBe('warning');
  });

  it('TypeScript: açık undefined default ile aynı davranır (parametre atlanmış gibi)', () => {
    const parsed = parseStrictRksvPublicEnvironment(undefined as unknown as string);
    expect(parsed.state).toBe(RksvPublicEnvironmentState.UNCONFIGURED);
  });
});

describe('parseStrictRksvPublicEnvironment — UTF-8 BOM (editor edge case)', () => {
  it('leading BOM + TEST still parses as TEST', () => {
    assertRksvParse(`\uFEFFTEST`, {
      input: '\uFEFFTEST',
      state: RksvPublicEnvironmentState.TEST,
      normalized: 'TEST',
      displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.test,
      alertType: 'warning',
      badgeColor: 'warning',
    });
  });

  it('BOM only after trim is empty → UNCONFIGURED', () => {
    assertRksvParse('\uFEFF', {
      input: '\uFEFF',
      state: RksvPublicEnvironmentState.UNCONFIGURED,
      normalized: null,
      displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.unconfigured,
      alertType: 'warning',
      badgeColor: 'warning',
    });
  });
});

describe('parseStrictRksvPublicEnvironment — ek kenar durumlar', () => {
  it('returns UNCONFIGURED for whitespace-only input', () => {
    assertRksvParse('   ', {
      input: '   ',
      state: RksvPublicEnvironmentState.UNCONFIGURED,
      normalized: null,
      displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.unconfigured,
      alertType: 'warning',
      badgeColor: 'warning',
    });
  });

  it('returns INVALID with sanitised raw for unknown uppercase value', () => {
    assertRksvParse('STAGING', {
      input: 'STAGING',
      state: RksvPublicEnvironmentState.INVALID,
      normalized: null,
      displayLabelKey: RKSV_ENV_I18N_KEYS.displayLabel.invalid,
      alertType: 'error',
      badgeColor: 'error',
      sanitizedRaw: 'STAGING',
    });
  });
});

describe('sanitizeRksvEnvironmentRawForDisplay', () => {
  it('replaces ASCII control characters', () => {
    expect(sanitizeRksvEnvironmentRawForDisplay('a\u0000b')).toBe('a\uFFFDb');
  });

  it('truncates very long values', () => {
    const long = 'x'.repeat(120);
    const out = sanitizeRksvEnvironmentRawForDisplay(long);
    expect(out.length).toBeLessThanOrEqual(97);
    expect(out.endsWith('…')).toBe(true);
  });
});
