import { formatUserDate, formatUserDateTime } from './dateFormatter';
import type { PosDailyClosingStatusDto } from '../services/api/shiftService';

export type DailyClosingStatusTranslate = (
  key: string,
  options?: Record<string, unknown>
) => string;

export function resolveAlreadyClosedDailyMessage(
  lastClosingPerformedAt: string | null | undefined,
  lastClosingDate: string | null | undefined,
  t: DailyClosingStatusTranslate
): string {
  if (lastClosingPerformedAt) {
    return t('settings:shift.dailyClosing.statusAlreadyClosedAt', {
      dateTime: formatUserDateTime(lastClosingPerformedAt),
    });
  }
  const closingDate = lastClosingDate ? formatUserDate(lastClosingDate) : '';
  if (closingDate) {
    return t('settings:shift.dailyClosing.statusAlreadyClosedOnDate', { date: closingDate });
  }
  return t('settings:shift.dailyClosing.statusAlreadyClosedToday');
}

export function resolveDailyClosingStatusMessage(
  status: PosDailyClosingStatusDto,
  t: DailyClosingStatusTranslate
): string {
  if (status.canClose) {
    return t('settings:shift.dailyClosing.statusCanClose');
  }

  switch (status.blockReason) {
    case 'already_closed_today':
      return resolveAlreadyClosedDailyMessage(
        status.lastClosingPerformedAt,
        status.lastClosingDate,
        t
      );
    case 'payments_without_invoice':
      return t('settings:shift.dailyClosing.statusPaymentsWithoutInvoice', {
        count: status.paymentsWithoutInvoiceCount,
      });
    case 'register_unavailable':
      return t('settings:shift.dailyClosing.statusRegisterUnavailable');
    case 'no_active_shift':
      return t('settings:shift.dailyClosing.statusNoActiveShift');
    default:
      return t('settings:shift.dailyClosing.statusBlocked');
  }
}
