/**
 * FA-side country formatting profiles. Backend CountryProfile remains the source of truth
 * for locale / currency / timezone; this map is a client mirror for display only.
 * Unknown codes fall back to Austria (Paket 13-b: de-DE / EUR / Europe/Vienna).
 */

export const COUNTRY_FORMAT_EMPTY = '—' as const;

export type CountryDateFormat = 'DD.MM.YYYY' | 'YYYY-MM-DD';

export type CountryFormatProfile = {
  code: string;
  locale: string;
  currency: string;
  timeZone: string;
  dateFormat: CountryDateFormat;
  decimalSeparator: string;
  thousandsSeparator: string;
};

export const AT_FORMAT_PROFILE: CountryFormatProfile = {
  code: 'AT',
  locale: 'de-DE',
  currency: 'EUR',
  timeZone: 'Europe/Vienna',
  dateFormat: 'DD.MM.YYYY',
  decimalSeparator: ',',
  thousandsSeparator: '.',
};

export const COUNTRY_FORMAT_PROFILES: Record<string, CountryFormatProfile> = {
  AT: AT_FORMAT_PROFILE,
  DE: {
    code: 'DE',
    locale: 'de-DE',
    currency: 'EUR',
    timeZone: 'Europe/Berlin',
    dateFormat: 'DD.MM.YYYY',
    decimalSeparator: ',',
    thousandsSeparator: '.',
  },
  CH: {
    code: 'CH',
    locale: 'de-CH',
    currency: 'CHF',
    timeZone: 'Europe/Zurich',
    dateFormat: 'DD.MM.YYYY',
    decimalSeparator: '.',
    thousandsSeparator: "'",
  },
  EU_DEFAULT: {
    code: 'EU_DEFAULT',
    locale: 'en',
    currency: 'EUR',
    timeZone: 'UTC',
    dateFormat: 'YYYY-MM-DD',
    decimalSeparator: '.',
    thousandsSeparator: ',',
  },
};

export function getCountryFormatProfile(countryCode?: string | null): CountryFormatProfile {
  const code = countryCode?.trim().toUpperCase();
  if (!code) return AT_FORMAT_PROFILE;
  return COUNTRY_FORMAT_PROFILES[code] ?? AT_FORMAT_PROFILE;
}

export type CountryDateInput = string | number | Date | null | undefined;

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

function isCalendarDateOnly(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(value.trim());
}

function parseDate(input: CountryDateInput): Date | null {
  if (input == null || input === '') return null;
  if (input instanceof Date) return Number.isNaN(input.getTime()) ? null : input;
  if (typeof input === 'number') {
    const d = new Date(input);
    return Number.isNaN(d.getTime()) ? null : d;
  }
  const trimmed = input.trim();
  if (isCalendarDateOnly(trimmed)) {
    const [y, m, d] = trimmed.split('-').map(Number);
    return new Date(y, m - 1, d);
  }
  const parsed = new Date(trimmed);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function formatPattern(year: number, month: number, day: number, pattern: CountryDateFormat): string {
  const y = String(year);
  const m = pad2(month);
  const d = pad2(day);
  if (pattern === 'YYYY-MM-DD') return `${y}-${m}-${d}`;
  return `${d}.${m}.${y}`;
}

function partsInZone(
  date: Date,
  timeZone: string
): { year: number; month: number; day: number; hour: number; minute: number; second: number } {
  const fmt = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });
  const map = Object.fromEntries(
    fmt.formatToParts(date).filter((p) => p.type !== 'literal').map((p) => [p.type, p.value])
  );
  return {
    year: Number(map.year),
    month: Number(map.month),
    day: Number(map.day),
    hour: Number(map.hour),
    minute: Number(map.minute),
    second: Number(map.second),
  };
}

export function formatCountryDate(
  input: CountryDateInput,
  profile: CountryFormatProfile = AT_FORMAT_PROFILE
): string {
  const parsed = parseDate(input);
  if (!parsed) return COUNTRY_FORMAT_EMPTY;
  if (typeof input === 'string' && isCalendarDateOnly(input.trim())) {
    return formatPattern(
      parsed.getFullYear(),
      parsed.getMonth() + 1,
      parsed.getDate(),
      profile.dateFormat
    );
  }
  const p = partsInZone(parsed, profile.timeZone);
  return formatPattern(p.year, p.month, p.day, profile.dateFormat);
}

export function formatCountryTime(
  input: CountryDateInput,
  profile: CountryFormatProfile = AT_FORMAT_PROFILE,
  options?: { includeSeconds?: boolean }
): string {
  const parsed = parseDate(input);
  if (!parsed) return COUNTRY_FORMAT_EMPTY;
  const p = partsInZone(parsed, profile.timeZone);
  const base = `${pad2(p.hour)}:${pad2(p.minute)}`;
  return options?.includeSeconds ? `${base}:${pad2(p.second)}` : base;
}

export function formatCountryDateTime(
  input: CountryDateInput,
  profile: CountryFormatProfile = AT_FORMAT_PROFILE,
  options?: { includeSeconds?: boolean }
): string {
  const parsed = parseDate(input);
  if (!parsed) return COUNTRY_FORMAT_EMPTY;
  const datePart = formatCountryDate(input, profile);
  const timePart = formatCountryTime(input, profile, options);
  if (datePart === COUNTRY_FORMAT_EMPTY || timePart === COUNTRY_FORMAT_EMPTY) {
    return COUNTRY_FORMAT_EMPTY;
  }
  return `${datePart} ${timePart}`;
}

export function formatCountryNumber(
  value: number,
  profile: CountryFormatProfile = AT_FORMAT_PROFILE,
  fractionDigits = 2
): string {
  if (!Number.isFinite(value)) return COUNTRY_FORMAT_EMPTY;
  const sign = value < 0 ? '-' : '';
  const abs = Math.abs(value);
  const [intRaw, frac = ''] = abs.toFixed(fractionDigits).split('.');
  const grouped = intRaw.replace(/\B(?=(\d{3})+(?!\d))/g, profile.thousandsSeparator);
  if (fractionDigits === 0) return `${sign}${grouped}`;
  return `${sign}${grouped}${profile.decimalSeparator}${frac}`;
}

export function formatCountryCurrency(
  value: number,
  profile: CountryFormatProfile = AT_FORMAT_PROFILE
): string {
  const amount = formatCountryNumber(value, profile, 2);
  if (amount === COUNTRY_FORMAT_EMPTY) return COUNTRY_FORMAT_EMPTY;
  if (profile.currency === 'CHF') return `CHF ${amount}`;
  if (profile.currency === 'EUR' && (profile.code === 'AT' || profile.code === 'DE')) {
    return `${amount} €`;
  }
  return `${amount} ${profile.currency}`;
}
