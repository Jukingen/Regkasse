import { describe, expect, it, vi } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n';
import SettingsPage from '@/app/(protected)/settings/page';

const mockUsePermissions = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
    usePermissions: () => mockUsePermissions(),
}));

vi.mock('@/features/settings/components/SuperAdminSettings', () => ({
    SuperAdminSettings: () => <div data-testid="super-admin-settings">SuperAdminSettings</div>,
}));

vi.mock('@/features/settings/components/ManagerSettings', () => ({
    ManagerSettings: () => <div data-testid="manager-settings">ManagerSettings</div>,
}));

describe('SettingsPage', () => {
    it('renders SuperAdminSettings when settings.manage is granted', () => {
        mockUsePermissions.mockReturnValue({
            hasPermission: (key: string) => key === 'settings.manage',
        });

        render(
            <I18nProvider>
                <SettingsPage />
            </I18nProvider>,
        );

        expect(screen.getByTestId('super-admin-settings')).toBeInTheDocument();
        expect(screen.queryByTestId('manager-settings')).not.toBeInTheDocument();
    });

    it('renders ManagerSettings without settings.manage', () => {
        mockUsePermissions.mockReturnValue({
            hasPermission: () => false,
        });

        render(
            <I18nProvider>
                <SettingsPage />
            </I18nProvider>,
        );

        expect(screen.getByTestId('manager-settings')).toBeInTheDocument();
        expect(screen.queryByTestId('super-admin-settings')).not.toBeInTheDocument();
    });
});
