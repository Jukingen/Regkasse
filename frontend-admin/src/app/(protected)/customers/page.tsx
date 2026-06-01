'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Button, Space, Input } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';
import CustomerList from '@/features/customers/components/CustomerList';
import CustomerForm from '@/features/customers/components/CustomerForm';
import { useCustomers, useCustomerFilters } from '@/features/customers/hooks/useCustomers';
import { Customer } from '@/api/generated/model';
import { useAdminBenefitAssignmentsList } from '@/api/admin/benefit-assignments';
import { customInstance } from '@/lib/axios';
import { useI18n } from '@/i18n';

/** Admin-only: fetches assignment count for display (assignment visibility). Same API as POS preview but distinct intent. */
async function getAdminCustomerAssignmentCount(customerId: string): Promise<number | null> {
    try {
        const body = await customInstance<{ data?: { assignedBenefitCount?: number } }>({
            url: `/api/Customer/${customerId}/benefit-summary`,
            method: 'GET',
        });
        const count = body?.data?.assignedBenefitCount;
        return typeof count === 'number' ? count : null;
    } catch (e: unknown) {
        const status = (e as { response?: { status?: number } })?.response?.status;
        if (status === 404) return null;
        throw e;
    }
}

export default function CustomersPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const router = useRouter();
    const { filters, setParam } = useCustomerFilters();
    const [isFormVisible, setIsFormVisible] = useState(false);
    const [editingCustomer, setEditingCustomer] = useState<Customer | null>(null);
    const [searchInput, setSearchInput] = useState('');
    useEffect(() => {
        setSearchInput(filters.search ?? '');
    }, [filters.search]);

    const {
        useList,
        useCreate,
        useUpdate,
        useDelete,
        invalidateList
    } = useCustomers();

    const { data: customers, isLoading, error } = useList({
        page: Number(filters.page) || 1,
        pageSize: Number(filters.pageSize) || 10,
        search: filters.search,
    });

    const { data: benefitAssignments } = useAdminBenefitAssignmentsList();

    const { data: assignmentCountForAdmin } = useQuery({
        queryKey: ['customer', editingCustomer?.id, 'admin-assignment-count'],
        queryFn: async () => {
            const id = editingCustomer?.id;
            if (!id) return 0;
            return getAdminCustomerAssignmentCount(id);
        },
        enabled: !!editingCustomer?.id,
    });

    // Mutations
    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    const handleCreate = async (values: Customer) => {
        try {
            await createMutation.mutateAsync({ data: values });
            message.success(t('customers.messages.created'));
            setIsFormVisible(false);
            invalidateList();
        } catch (err) {
            message.error(t('customers.messages.createFailed'));
        }
    };

    const handleUpdate = async (values: Customer) => {
        if (!editingCustomer?.id) return;
        try {
            await updateMutation.mutateAsync({ id: editingCustomer.id, data: values });
            message.success(t('customers.messages.updated'));
            setIsFormVisible(false);
            setEditingCustomer(null);
            invalidateList();
        } catch (err) {
            message.error(t('customers.messages.updateFailed'));
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success(t('customers.messages.deleted'));
            invalidateList();
        } catch (err) {
            message.error(t('customers.messages.deleteFailed'));
        }
    };

    const openCreateModal = () => {
        setEditingCustomer(null);
        setIsFormVisible(true);
    };

    const openEditModal = (customer: Customer) => {
        setEditingCustomer(customer);
        setIsFormVisible(true);
    };

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title={ADMIN_NAV_LABELS.customers}
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.customers }]}
                actions={
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>
                        {t('customers.page.newCustomer')}
                    </Button>
                }
            >
                <Input.Search
                    placeholder={t('customers.page.searchPlaceholder')}
                    value={searchInput}
                    onChange={(e) => {
                        const v = e.target.value;
                        setSearchInput(v);
                        if (!v) setParam('search', undefined);
                    }}
                    onSearch={(val) => setParam('search', val || undefined)}
                    style={{ width: 300 }}
                    allowClear
                    enterButton={<SearchOutlined />}
                />
            </AdminPageHeader>

            <AdminDataList
                isLoading={isLoading}
                isError={!!error}
                error={error as Error}
                isEmpty={(customers ?? []).length === 0}
            >
                <CustomerList
                    data={customers ?? []}
                    loading={isLoading}
                    onEdit={openEditModal}
                    onDelete={handleDelete}
                    onManageBenefits={(customer) => {
                        if (customer.id) router.push(`/benefit-assignments?customerId=${customer.id}`);
                    }}
                    benefitAssignments={benefitAssignments ?? undefined}
                />
            </AdminDataList>

            <CustomerForm
                visible={isFormVisible}
                initialValues={editingCustomer}
                onCancel={() => setIsFormVisible(false)}
                onSubmit={editingCustomer ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
                assignedBenefitCount={assignmentCountForAdmin ?? undefined}
            />
        </Space>
    );
}
