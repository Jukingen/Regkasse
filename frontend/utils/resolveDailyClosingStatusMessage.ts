import { formatUserDate } from './dateFormatter';
import type { PosDailyClosingStatusDto } from '../services/api/shiftService';

export type DailyClosingStatusTranslate = (
  key: string,
  options?: Record<string, unknown>
) => string;

export function resolveDailyClosingStatusMessage(
  status: PosDailyClosingStatusDto,
  t: DailyClosingStatusTranslate
): string {
  if (status.canClose) {
    return t('settings:shift.dailyClosing.statusCanClose');
  }

  switch (status.blockReason) {
    case 'already_closed_today': {
      const closingDate = status.lastClosingDate
        ? formatUserDate(status.lastClosingDate)
        : '';
      if (closingDate) {
        return t('settings:shift.dailyClosing.statusAlreadyClosedOnDate', { date: closingDate });
      }
      return t('settings:shift.dailyClosing.statusAlreadyClosedToday');
    }
    case 'payments_without_invoice':
      return t('settings:shift.dailyClosing.statusPaymentsWithoutInvoice', {
        count: status.paymentsWithoutInvoiceCount,
      });
    case 'register_unavailable':
      return t('settings:shift.dailyClosing.statusRegisterUnavailable');
    default:
      return t('settings:shift.dailyClosing.statusBlocked');
  }
}
