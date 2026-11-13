import { describe, expect, it } from 'vitest';

import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';
import {
  finanzOnlineRetryUiPresentation,
  getFinanzOnlineRetryUiState,
  isFinanzOnlineRetryButtonContract,
} from '@/shared/foReconciliationRowTriage';

describe('getFinanzOnlineRetryUiState', () => {
  it('returns empty for nullish or blank', () => {
    expect(getFinanzOnlineRetryUiState(undefined)).toBe('empty');
    expect(getFinanzOnlineRetryUiState(null)).toBe('empty');
    expect(getFinanzOnlineRetryUiState('   ')).toBe('empty');
  });

  it('maps retry-button statuses', () => {
    expect(getFinanzOnlineRetryUiState('Pending')).toBe('retry_available');
    expect(getFinanzOnlineRetryUiState('Failed')).toBe('retry_available');
    expect(getFinanzOnlineRetryUiState('NeedsReconciliation')).toBe('retry_available');
  });

  it('maps Submitted separately', () => {
    expect(getFinanzOnlineRetryUiState('Submitted')).toBe('submitted_no_retry');
  });

  it('does not infer retry for unknown strings', () => {
    expect(getFinanzOnlineRetryUiState('Success')).toBe('other_status');
    expect(getFinanzOnlineRetryUiState('Whatever')).toBe('other_status');
  });

  it('contract: backend status "Success" is not treated as retry_available (no Erneut senden mirror)', () => {
    expect(getFinanzOnlineRetryUiState('Success')).toBe('other_status');
    expect(finanzOnlineRetryUiPresentation('other_status').tooltip).toMatch(/Retry-Button-Liste/i);
  });
});

describe('isFinanzOnlineRetryButtonContract', () => {
  it('requires non-empty paymentId and retry_available status', () => {
    const ok: FinanzOnlineReconciliationItemDto = {
      paymentId: '22222222-2222-4222-8222-222222222222',
      finanzOnlineStatus: 'Failed',
    };
    expect(isFinanzOnlineRetryButtonContract(ok)).toBe(true);
  });

  it('is false when paymentId missing', () => {
    expect(
      isFinanzOnlineRetryButtonContract({
        finanzOnlineStatus: 'Failed',
      })
    ).toBe(false);
  });

  it('is false when status is Submitted', () => {
    expect(
      isFinanzOnlineRetryButtonContract({
        paymentId: '22222222-2222-4222-8222-222222222222',
        finanzOnlineStatus: 'Submitted',
      })
    ).toBe(false);
  });

  it('matches Erneut senden status set exactly', () => {
    expect(
      isFinanzOnlineRetryButtonContract({ paymentId: 'p', finanzOnlineStatus: 'Pending' })
    ).toBe(true);
    expect(
      isFinanzOnlineRetryButtonContract({
        paymentId: 'p',
        finanzOnlineStatus: 'NeedsReconciliation',
      })
    ).toBe(true);
    expect(
      isFinanzOnlineRetryButtonContract({ paymentId: 'p', finanzOnlineStatus: 'Success' })
    ).toBe(false);
  });
});

describe('finanzOnlineRetryUiPresentation', () => {
  it('returns German labels for each state', () => {
    const r = finanzOnlineRetryUiPresentation('retry_available');
    expect(r.tagLabel).toBe('Retry-UI');
    expect(r.tagColor).toBe('blue');
    expect(r.tooltip).toContain('Erneut senden');

    const s = finanzOnlineRetryUiPresentation('submitted_no_retry');
    expect(s.tagLabel).toBe('Eingereicht');

    const o = finanzOnlineRetryUiPresentation('other_status');
    expect(o.tagLabel).toBe('Sonstiger Status');

    const e = finanzOnlineRetryUiPresentation('empty');
    expect(e.tagLabel).toBe('—');
  });
});
