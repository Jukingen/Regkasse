'use client';

import { useCallback, useState } from 'react';

import type { UserTenantAssignmentRow } from '@/features/users/components/UserTenantAssignmentModal';

export type TenantAssignmentModalOpenArgs = {
    userId: string;
    userEmail: string;
    existingTenants?: UserTenantAssignmentRow[];
    initialSelectedTenantIds?: string[];
};

type TenantAssignmentModalState = {
    visible: boolean;
    userId: string | null;
    userEmail: string;
    userTenants: UserTenantAssignmentRow[];
    initialSelectedTenantIds: string[];
};

export type UseTenantAssignmentModalOptions = {
    onEditRequested?: (userId: string) => void;
};

const INITIAL_STATE: TenantAssignmentModalState = {
    visible: false,
    userId: null,
    userEmail: '',
    userTenants: [],
    initialSelectedTenantIds: [],
};

/** Tenant assignment modal açılma koşullarını tek yerde toplar. */
export function useTenantAssignmentModal(options: UseTenantAssignmentModalOptions = {}) {
    const [state, setState] = useState<TenantAssignmentModalState>(INITIAL_STATE);

    const closeModal = useCallback(() => {
        setState(INITIAL_STATE);
    }, []);

    const openModal = useCallback(
        ({ userId, userEmail, existingTenants = [], initialSelectedTenantIds = [] }: TenantAssignmentModalOpenArgs) => {
            if (existingTenants.length > 0) {
                options.onEditRequested?.(userId);
                return false;
            }

            setState({
                visible: true,
                userId,
                userEmail,
                userTenants: existingTenants,
                initialSelectedTenantIds,
            });
            return true;
        },
        [options],
    );

    return {
        visible: state.visible,
        userId: state.userId,
        userEmail: state.userEmail,
        userTenants: state.userTenants,
        initialSelectedTenantIds: state.initialSelectedTenantIds,
        openModal,
        closeModal,
    };
}
