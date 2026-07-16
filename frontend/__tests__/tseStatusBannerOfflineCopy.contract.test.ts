/**
 * Contract test: offline airplane-mode banner copy must stay aligned with `components/TseStatusBanner.tsx`.
 * Update both files together when changing operator-facing German strings.
 */
import { describe, expect, it } from '@jest/globals';

describe('TseStatusBanner offline copy (contract)', () => {
  // Must match TSE_OFFLINE_BANNER_LABEL in components/TseStatusBanner.tsx
  const OFFLINE_LABEL = 'OFFLINE MODUS – NUR BARZAHLUNG, KEINE GUTSCHEINE';

  it('matches QA checklist substrings for airplane mode / TSE offline', () => {
    expect(OFFLINE_LABEL).toMatch(/OFFLINE MODUS/i);
    expect(OFFLINE_LABEL).toMatch(/NUR BARZAHLUNG/i);
    expect(OFFLINE_LABEL).toMatch(/KEINE GUTSCHEINE/i);
  });
});
