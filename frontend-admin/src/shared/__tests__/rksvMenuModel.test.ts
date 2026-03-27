import { describe, expect, it } from 'vitest';
import { buildRksvMenuGroups } from '@/shared/rksvMenuModel';

/** Stable route contract for sidebar + hub: keys and hrefs must match MENU_PERMISSION and App Router paths. */
const EXPECTED_KEYS_AND_HREFS: { key: string; href: string }[] = [
    { key: '/rksv/operations', href: '/rksv' },
    { key: '/rksv/finanz-online-outbox', href: '/rksv/finanz-online-outbox' },
    { key: '/rksv/finanz-online-queue', href: '/rksv/finanz-online-queue' },
    { key: '/rksv/incident', href: '/rksv/incident' },
    { key: '/rksv/replay-batch', href: '/rksv/replay-batch' },
    { key: '/rksv/payload-hash-conflicts', href: '/rksv/payload-hash-conflicts' },
    { key: '/rksv/verifications', href: '/rksv/verifications' },
    { key: '/rksv/fiscal-export-diagnostics', href: '/rksv/fiscal-export-diagnostics' },
    { key: '/rksv/integrity', href: '/rksv/integrity' },
    { key: '/rksv/offline-intent-coverage', href: '/rksv/offline-intent-coverage' },
    { key: '/rksv/status', href: '/rksv/status' },
    { key: '/rksv/cmc-certificate', href: '/rksv/cmc-certificate' },
    { key: '/rksv/finanz-online-operations', href: '/rksv/finanz-online-operations' },
];

describe('buildRksvMenuGroups', () => {
    it('keeps menu keys and hrefs aligned with routes (labels may change)', () => {
        const groups = buildRksvMenuGroups('Audit test label');
        const flat = groups.flatMap((g) => g.items);
        expect(flat.map((i) => ({ key: i.key, href: i.href }))).toEqual(EXPECTED_KEYS_AND_HREFS);
    });

    it('injects the verifications nav label from operator copy', () => {
        const label = 'Audit-Spur (Signatur/Offline)';
        const groups = buildRksvMenuGroups(label);
        const v = groups.flatMap((g) => g.items).find((i) => i.key === '/rksv/verifications');
        expect(v?.label).toBe(label);
    });

    it('exposes four stable group ids for layout + hub', () => {
        const ids = buildRksvMenuGroups('x').map((g) => g.id);
        expect(ids).toEqual(['daily', 'investigation', 'diagnostics', 'config']);
    });

    it('includes hub task lines for landing panels (sidebar ignores them)', () => {
        const groups = buildRksvMenuGroups('x');
        for (const g of groups) {
            expect(g.hubTaskLine.trim().length).toBeGreaterThan(20);
        }
    });
});
