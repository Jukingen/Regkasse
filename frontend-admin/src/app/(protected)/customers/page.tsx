'use client';

import React, { useState } from 'react';
import { Button, message, Space, Input } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminDataList } from '@/components/admin-layout/AdminDataList';
import CustomerList from '@/features/customers/components/CustomerList';
import CustomerForm from '@/features/customers/components/CustomerForm';
import { useCustomers, useCustomerFilters } from '@/features/customers/hooks/useCustomers';
import { Customer } from '@/api/generated/model';

export default function CustomersPage() {
    const { filters, setParam } = useCustomerFilters();
    const [isFormVisible, setIsFormVisible] = useState(false);
    const [editingCustomer, setEditingCustomer] = useState<Customer | null>(null);

    const {
        useList,
        useCreate,
        useUpdate,
        useDelete,
        invalidateList
    } = useCustomers();

    // Queries
    const { data: customers, isLoading, error } = useList({
        page: Number(filters.page) || 1,
        pageSize: Number(filters.pageSize) || 10,
    });

    // Mutations
    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    const handleCreate = async (values: Customer) => {
        try {
            await createMutation.mutateAsync({ data: values });
            message.success('Customer created successfully');
            setIsFormVisible(false);
            invalidateList();
        } catch (err) {
            message.error('Failed to create customer');
        }
    };

    const handleUpdate = async (values: Customer) => {
        if (!editingCustomer?.id) return;
        try {
            await updateMutation.mutateAsync({ id: editingCustomer.id, data: values });
            message.success('Customer updated successfully');
            setIsFormVisible(false);
            setEditingCustomer(null);
            invalidateList();
        } catch (err) {
            message.error('Failed to update customer');
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Customer deleted successfully');
            invalidateList();
        } catch (err) {
            message.error('Failed to delete customer');
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
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title="Customers"
                breadcrumbs={[{ title: 'Dashboard', href: '/' }, { title: 'Customers' }]}
                actions={
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>
                        New Customer
                    </Button>
                }
            >
                <Input.Search
                    placeholder="Search customers..."
                    onSearch={(val) => setParam('search', val)}
                    style={{ width: 300 }}
                    allowClear
                />
            </AdminPageHeader>

            <AdminDataList
                isLoading={isLoading}
                isError={!!error}
                error={error as Error}
                isEmpty={!customers || customers.length === 0}
            >
                <CustomerList
                    data={customers || []}
                    loading={isLoading}
                    onEdit={openEditModal}
                    onDelete={handleDelete}
                />
            </AdminDataList>

            <CustomerForm
                visible={isFormVisible}
                initialValues={editingCustomer}
                onCancel={() => setIsFormVisible(false)}
                onSubmit={editingCustomer ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />
        </Space>
    );
}
