/**
 * Date/number formatting helpers.
 * Dates: fixed German display DD.MM.YYYY — independent of formatLocale.
 * Numbers: still use BCP-47 formatLocale (de-AT).
 */

import { formatUserDate, formatUserDateTime, formatUserTime } from '../utils/dateFormatter';

export function formatDateTime(
  input: string | number | Date | null | undefined,
  _formatLocale: string,
  options?: { includeSeconds?: boolean },
): string {
  if (input == null || input === '') return '';
  return formatUserDateTime(input, { includeSeconds: options?.includeSeconds ?? true });
}

export function formatDate(
  input: string | number | Date | null | undefined,
  _formatLocale?: string,
): string {
  if (input == null || input === '') return '';
  return formatUserDate(input);
}

export function formatTime(
  input: string | number | Date | null | undefined,
  _formatLocale: string,
  options?: { includeSeconds?: boolean },
): string {
  if (input == null || input === '') return '';
  return formatUserTime(input, { includeSeconds: options?.includeSeconds });
}

export function formatNumber(value: number, formatLocale: string, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(formatLocale, options).format(value);
}
