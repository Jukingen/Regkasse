'use client';

import { useMemo, useState, type ReactNode } from 'react';
import { Avatar, Button, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { IdcardOutlined, KeyOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';

import { VOLUNTARY_CHANGE_PASSWORD_PATH } from '@/features/auth/constants/changePasswordRoute';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { SelfServiceUsernameModal } from '@/features/user/components/SelfServiceUsernameModal';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import type { AuthUser } from '@/shared/auth/types';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';


export function buildAdminHeaderUserLabel(
    user: AuthUser | null | undefined,
    fallbackLabel: string,
): ReactNode {
    const role = user?.role?.trim();
    const userName = user?.userName?.trim() || user?.email?.trim();

    if (!role && !userName) {
        return fallbackLabel;
    }

    return (
        <span className="admin-header-user-identity">
            {role ? <span className="admin-header-user-role">{role}</span> : null}
            {userName ? <span className="admin-header-user-name">{userName}</span> : null}
        </span>
    );
}

type AdminHeaderUserMenuProps = {
    user: AuthUser | null | undefined;
    fallbackLabel: string;
    isMobile: boolean;
    onLogout: () => void;
};

export function AdminHeaderUserMenu({
    user,
    fallbackLabel,
    isMobile,
    onLogout,
}: AdminHeaderUserMenuProps) {
    const router = useRouter();
    const { logout } = useAuth();
    const { t } = useI18n();
    const [usernameModalOpen, setUsernameModalOpen] = useState(false);

    const userLabel = useMemo(
        () => buildAdminHeaderUserLabel(user, fallbackLabel),
        [user, fallbackLabel],
    );

    const menuItems: MenuProps['items'] = useMemo(
        () => [
            {
                key: 'profile',
                icon: <UserOutlined />,
                label: t(ADMIN_NAV_LABEL_KEYS.myProfile),
                onClick: () => router.push('/profile'),
            },
            {
                key: 'change-username',
                icon: <IdcardOutlined />,
                label: t(ADMIN_NAV_LABEL_KEYS.changeUsername),
                onClick: () => setUsernameModalOpen(true),
            },
            {
                key: 'change-password',
                icon: <KeyOutlined />,
                label: t(ADMIN_NAV_LABEL_KEYS.changePassword),
                onClick: () => router.push(VOLUNTARY_CHANGE_PASSWORD_PATH),
            },
            { type: 'divider' as const },
            {
                key: 'logout',
                icon: <LogoutOutlined />,
                label: t(ADMIN_NAV_LABEL_KEYS.logout),
                danger: true,
                onClick: onLogout,
            },
        ],
        [onLogout, router, t],
    );

    const handleUsernameChanged = async () => {
        await logout({ silent: false, redirectTo: '/login' });
    };

    return (
        <>
            <Dropdown
                menu={{ items: menuItems }}
                placement="bottomRight"
                trigger={['click']}
                classNames={{ root: 'admin-header-dropdown' }}
                getPopupContainer={getAdminHeaderPopupContainer}
            >
                <Button
                    type="default"
                    size="small"
                    className="admin-header-tool-btn admin-header-user-menu-btn"
                    aria-label={typeof userLabel === 'string' ? userLabel : fallbackLabel}
                >
                    <Avatar size="small" icon={<UserOutlined />} className="admin-header-user-avatar" />
                    {!isMobile ? userLabel : null}
                </Button>
            </Dropdown>

            <SelfServiceUsernameModal
                open={usernameModalOpen}
                currentUsername={user?.userName?.trim() ?? ''}
                userEmail={user?.email}
                onClose={() => setUsernameModalOpen(false)}
                onSuccess={() => void handleUsernameChanged()}
            />
        </>
    );
}
