import { describe, expect, it } from 'vitest';

import { computeDepExportStats } from '@/features/rksv/types/depExport';

describe('computeDepExportStats', () => {
  it('returns null when export is missing', () => {
    expect(computeDepExportStats(null)).toBeNull();
    expect(computeDepExportStats(undefined)).toBeNull();
  });

  it('counts groups and compact signatures', () => {
    const stats = computeDepExportStats({
      'Belege-Gruppe': [
        {
          Signaturzertifikat: 'cert-a-base64',
          Zertifizierungsstellen: ['ca-1'],
          'Belege-kompakt': ['a.b.c', 'd.e.f'],
        },
        {
          Signaturzertifikat: 'cert-b-base64',
          Zertifizierungsstellen: [],
          'Belege-kompakt': ['g.h.i'],
        },
      ],
    });

    expect(stats).toEqual({
      groupCount: 2,
      totalSignatures: 3,
      certificateThumbprints: ['cert-a-base64', 'cert-b-base64'],
    });
  });
});
