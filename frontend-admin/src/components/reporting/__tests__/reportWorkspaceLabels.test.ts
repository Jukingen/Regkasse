import { describe, expect, it } from 'vitest';

import {
  documentStatusVisual,
  matchesReportDocFilter,
  matchesSubmissionFilter,
  submissionLifecycleVisual,
} from '@/components/reporting/reportWorkspaceLabels';

describe('reportWorkspaceLabels', () => {
  it('maps document statuses', () => {
    expect(documentStatusVisual('Provisional').labelKey).toBe('docProvisional');
    expect(documentStatusVisual('Finalized').labelKey).toBe('docFinalized');
    expect(documentStatusVisual('Superseded').labelKey).toBe('docSuperseded');
  });

  it('maps submission lifecycles', () => {
    expect(submissionLifecycleVisual('not_submitted').labelKey).toBe('subNotSubmitted');
    expect(submissionLifecycleVisual('accepted').labelKey).toBe('subAccepted');
    expect(submissionLifecycleVisual('queued').labelKey).toBe('subInFlight');
    expect(submissionLifecycleVisual('awaiting_protocol').labelKey).toBe('subInFlight');
    expect(submissionLifecycleVisual('retry_pending').labelKey).toBe('subInFlight');
  });

  it('filters submission groups', () => {
    expect(matchesSubmissionFilter('not_submitted', 'notSubmitted')).toBe(true);
    expect(matchesSubmissionFilter('queued', 'inFlight')).toBe(true);
    expect(matchesSubmissionFilter('accepted', 'accepted')).toBe(true);
    expect(matchesSubmissionFilter('rejected', 'rejectedOrReview')).toBe(true);
    expect(matchesSubmissionFilter('correction_required', 'rejectedOrReview')).toBe(true);
  });

  it('filters report doc', () => {
    expect(matchesReportDocFilter('Finalized', 'Finalized')).toBe(true);
    expect(matchesReportDocFilter('Provisional', 'all')).toBe(true);
  });
});
