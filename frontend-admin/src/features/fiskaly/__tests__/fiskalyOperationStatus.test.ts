import { describe, expect, it } from 'vitest';

import {
  canRetryFiskalyHistoryStatus,
  fiskalyHistoryProgressPercent,
  isFiskalyHistoryInFlight,
  isFiskalyHistoryTerminal,
  normalizeFiskalyLiveStatus,
} from '../fiskalyOperationStatus';

describe('fiskalyOperationStatus', () => {
  it('maps in-flight and terminal states', () => {
    expect(isFiskalyHistoryInFlight('Pending')).toBe(true);
    expect(isFiskalyHistoryInFlight('Processing')).toBe(true);
    expect(isFiskalyHistoryInFlight('Success')).toBe(false);
    expect(isFiskalyHistoryTerminal('Success')).toBe(true);
    expect(isFiskalyHistoryTerminal('Failed')).toBe(true);
    expect(isFiskalyHistoryTerminal('Processing')).toBe(false);
  });

  it('allows retry only for failed or pending', () => {
    expect(canRetryFiskalyHistoryStatus('Failed')).toBe(true);
    expect(canRetryFiskalyHistoryStatus('Pending')).toBe(true);
    expect(canRetryFiskalyHistoryStatus('Processing')).toBe(false);
    expect(canRetryFiskalyHistoryStatus('Success')).toBe(false);
  });

  it('computes progress percent and prefers explicit values', () => {
    expect(fiskalyHistoryProgressPercent('Pending')).toBe(5);
    expect(fiskalyHistoryProgressPercent('Processing')).toBe(55);
    expect(fiskalyHistoryProgressPercent('Success')).toBe(100);
    expect(fiskalyHistoryProgressPercent('Failed')).toBe(100);
    expect(fiskalyHistoryProgressPercent('Processing', 80)).toBe(80);
  });

  it('normalizes Success/Completed to Success live status', () => {
    expect(normalizeFiskalyLiveStatus('Completed')).toBe('Success');
    expect(normalizeFiskalyLiveStatus('Success')).toBe('Success');
    expect(normalizeFiskalyLiveStatus('Processing')).toBe('Processing');
  });
});
