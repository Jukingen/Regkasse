import { describe, expect, it } from 'vitest';
import { buildRksvMenuGroups, getRksvOpenSubgroupKeys } from '@/shared/rksvMenuModel';

/** Hub-only leaves (top-level RKSV items live in `adminSidebarRegistry` to avoid duplicate menu keys). */
const EXPECTED_KEYS_AND_HREFS: { key: string; href: string }[] = [
    { key: '/rksv/finanz-online-queue', href: '/rksv/finanz-online-queue' },
    { key: '/rksv/incident', href: '/rksv/incident' },
    { key: '/rksv/replay-batch', href: '/rksv/replay-batch' },
    { key: '/rksv/payload-hash-conflicts', href: '/rksv/payload-hash-conflicts' },
    { key: '/rksv/verifications', href: '/rksv/verifications' },
    { key: '/rksv/fiscal-export-diagnostics', href: '/rksv/fiscal-export-diagnostics' },
    { key: '/rksv/integrity', href: '/rksv/integrity' },
    { key: '/rksv/compliance', href: '/rksv/compliance' },
    { key: '/rksv/signature-chain', href: '/rksv/signature-chain' },
    { key: '/rksv/offline-intent-coverage', href: '/rksv/offline-intent-coverage' },
    { key: '/rksv/belegcheck', href: '/rksv/belegcheck' },
    { key: '/rksv/cmc-certificate', href: '/rksv/cmc-certificate' },
    { key: '/rksv/finanz-online-operations', href: '/rksv/finanz-online-operations' },
];

const passthroughT = (key: string) => key;

describe('buildRksvMenuGroups', () => {
    it('keeps menu keys and hrefs aligned with routes (labels may change)', () => {
        const groups = buildRksvMenuGroups(passthroughT, 'Audit test label');
        const flat = groups.flatMap((g) => g.items);
        expect(flat.map((i) => ({ key: i.key, href: i.href }))).toEqual(EXPECTED_KEYS_AND_HREFS);
    });

    it('injects the verifications nav label from operator copy', () => {
        const label = 'Audit-Spur (Signatur/Offline)';
        const groups = buildRksvMenuGroups(passthroughT, label);
        const v = groups.flatMap((g) => g.items).find((i) => i.key === '/rksv/verifications');
        expect(v?.label).toBe(label);
    });

    it('exposes four stable group ids for layout + hub', () => {
        const ids = buildRksvMenuGroups(passthroughT, 'x').map((g) => g.id);
        expect(ids).toEqual(['daily', 'investigation', 'diagnostics', 'config']);
    });

    it('includes hub task lines for landing panels (sidebar ignores them)', () => {
        const groups = buildRksvMenuGroups(passthroughT, 'x');
        for (const g of groups) {
            expect(g.hubTaskLine.trim().length).toBeGreaterThan(20);
        }
    });

    it('does not treat the /rksv hub href as a prefix of deeper /rksv/* routes', () => {
        const groups = buildRksvMenuGroups(passthroughT, 'x');
        expect(getRksvOpenSubgroupKeys('/rksv/incident', groups)).toEqual(['rksv-grp-investigation']);
        expect(getRksvOpenSubgroupKeys('/rksv', groups)).toEqual(['rksv-grp-daily']);
    });
});
