import { describe, expect, it, vi } from 'vitest';
import { buildDefaultCommands } from '@/features/command-palette/defaultCommands';

describe('buildDefaultCommands', () => {
    it('includes pinned pages and actions with router navigation', () => {
        const push = vi.fn();
        const closePalette = vi.fn();
        const items = buildDefaultCommands({
            t: (k) => k,
            router: { push } as never,
            closePalette,
            triggerBackup: vi.fn(),
        });

        const dashboard = items.find((i) => i.id === 'page:dashboard');
        expect(dashboard?.type).toBe('page');
        dashboard?.action();
        expect(closePalette).toHaveBeenCalled();
        expect(push).toHaveBeenCalledWith('/dashboard');

        expect(items.some((i) => i.id === 'action:create-user' && i.type === 'action')).toBe(true);
        expect(items.some((i) => i.id === 'action:create-register')).toBe(true);
    });
});
