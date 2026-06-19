import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ChangeRoleModal } from '@/features/users/components/ChangeRoleModal';

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string, params?: Record<string, string>) => {
            if (key === 'users.roleChange.description' && params) {
                return `${params.oldRole} -> ${params.newRole}`;
            }
            if (key.startsWith('users.roleChange.')) return key;
        },
    }),
}));

describe('ChangeRoleModal', () => {
    it('calls onConfirm with preserve flag when checkbox is checked', () => {
        const onConfirm = vi.fn();
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="Cashier"
                onCancel={vi.fn()}
                onConfirm={onConfirm}
            />,
        );

        fireEvent.click(screen.getByRole('checkbox'));
        fireEvent.click(screen.getByText('users.roleChange.confirm'));

        expect(onConfirm).toHaveBeenCalledWith(true);
    });

    it('defaults preserve flag to false', () => {
        const onConfirm = vi.fn();
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="Cashier"
                onCancel={vi.fn()}
                onConfirm={onConfirm}
            />,
        );

        fireEvent.click(screen.getByText('users.roleChange.confirm'));
        expect(onConfirm).toHaveBeenCalledWith(false);
    });

    it('shows no previous role message and hides checkbox', () => {
        render(
            <ChangeRoleModal
                open
                previousRole=""
                newRole="Cashier"
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
            />,
        );

        expect(screen.getByText('users.roleChange.noPreviousRoleTitle')).toBeTruthy();
        expect(screen.queryByRole('checkbox')).toBeNull();
    });

    it('shows SuperAdmin warning and hides checkbox', () => {
        render(
            <ChangeRoleModal
                open
                previousRole="SuperAdmin"
                newRole="Manager"
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
            />,
        );

        expect(screen.getByText('users.roleChange.superAdminWarningTitle')).toBeTruthy();
        expect(screen.queryByRole('checkbox')).toBeNull();
    });

    it('shows checkbox for Manager to custom role change', () => {
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="CustomBarStaff"
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
            />,
        );

        expect(screen.getByRole('checkbox')).toBeTruthy();
        expect(screen.queryByText('users.roleChange.superAdminWarningTitle')).toBeNull();
    });

    it('hides checkbox when tenant context is missing', () => {
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="Cashier"
                hasTenantContext={false}
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
            />,
        );

        expect(screen.queryByRole('checkbox')).toBeNull();
    });

    it('passes false preserve flag when tenant context is missing', () => {
        const onConfirm = vi.fn();
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="Cashier"
                hasTenantContext={false}
                onCancel={vi.fn()}
                onConfirm={onConfirm}
            />,
        );

        fireEvent.click(screen.getByText('users.roleChange.confirm'));
        expect(onConfirm).toHaveBeenCalledWith(false);
    });

    it('disables confirm when role is unchanged', () => {
        render(
            <ChangeRoleModal
                open
                previousRole="Manager"
                newRole="Manager"
                onCancel={vi.fn()}
                onConfirm={vi.fn()}
            />,
        );

        const confirmButton = screen.getByText('users.roleChange.confirm').closest('button');
        expect(confirmButton?.disabled).toBe(true);
    });
});
