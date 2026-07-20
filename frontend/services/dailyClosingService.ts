/**
 * POS Tagesabschluss (daily closing) API facade.
 * Canonical route: POST /api/pos/shift/daily-closing (not /api/pos/daily-closing).
 */
import {
  DailyClosingApiError,
  downloadDailyClosingReportPdf,
  fetchDailyClosingStatus,
  performDailyClosingApi,
  type PosDailyClosingResult,
  type PosDailyClosingStatusDto,
} from './api/shiftService';
import {
  extractDailyClosingErrorDetails,
  getDailyClosingErrorMessage,
} from '../utils/errorMessages';

export {
  DailyClosingApiError,
  downloadDailyClosingReportPdf,
  type PosDailyClosingResult,
  type PosDailyClosingStatusDto,
};

export type PerformDailyClosingParams = {
  cashCount: number;
  notes?: string;
};

/**
 * Readiness for fiscal Tagesabschluss on the active shift register.
 */
export async function getDailyClosingStatus(): Promise<PosDailyClosingStatusDto> {
  try {
    const status = await fetchDailyClosingStatus();
    if (__DEV__) {
      console.log('✅ Tagesabschluss status:', {
        canClose: status.canClose,
        blockReason: status.blockReason,
        hasActiveShift: status.hasActiveShift,
      });
    }
    return status;
  } catch (error) {
    if (__DEV__) {
      console.error('❌ Tagesabschluss status failed:', error);
    }
    throw error;
  }
}

/**
 * Perform POS fiscal daily closing for the active shift.
 * Body: { cashCount, notes? } — register comes from the active CashierShift server-side.
 */
export async function performDailyClosing(
  params: PerformDailyClosingParams
): Promise<PosDailyClosingResult> {
  try {
    const result = await performDailyClosingApi(params.cashCount, params.notes);
    if (__DEV__) {
      console.log('✅ Tagesabschluss success:', {
        success: result.success,
        dailyClosingId: result.dailyClosingId,
        totalSales: result.report?.totalSales,
      });
    }
    return result;
  } catch (error) {
    const details = extractDailyClosingErrorDetails(error);
    const userMessage = getDailyClosingErrorMessage(details.code, { count: details.count });
    if (__DEV__) {
      console.error('❌ Tagesabschluss failed:', {
        error,
        errorCode: details.code,
        userMessage,
        technicalMessage: details.technicalMessage,
      });
    }
    // Re-throw so UI (ShiftManager) can Alert + show modal error.
    throw error instanceof DailyClosingApiError
      ? error
      : new DailyClosingApiError(userMessage, {
          code: details.code,
          paymentsWithoutInvoiceCount: details.count,
        });
  }
}

/**
 * User-facing German message for a daily-closing failure (settings / modal).
 */
export function resolveDailyClosingUserMessage(error: unknown): string {
  const details = extractDailyClosingErrorDetails(error);
  return getDailyClosingErrorMessage(details.code, { count: details.count });
}
