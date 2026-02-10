'use client';

import React from 'react';
import { Table, Card, Typography, Tag, Space, Button, Avatar } from 'antd';
import { UserOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useGetApiUserManagement } from '@/api/generated/user-management/user-management';
import type { UserResponse } from '@/api/generated/model';

const { Title } = Typography;

export default function UsersPage() {
    const { data: users, isLoading } = useGetApiUserManagement();

    const columns = [
        {
            title: 'User',
            key: 'user',
            render: (_: any, record: UserResponse) => (
                <Space>
                    <Avatar icon={<UserOutlined />} />
                    <div>
                        <div style={{ fontWeight: 'bold' }}>{record.fullName}</div>
                        <div style={{ fontSize: '12px', color: '#999' }}>{record.email}</div>
                    </div>
                </Space>
            ),
        },
        {
            title: 'Role',
            dataIndex: 'role',
            key: 'role',
            render: (role: string) => <Tag color="gold">{role}</Tag>,
        },
        {
            title: 'Branch',
            dataIndex: 'branchName',
            key: 'branchName',
        },
        {
            title: 'Status',
            dataIndex: 'isActive',
            key: 'status',
            render: (active: boolean) => (
                <Tag color={active ? 'green' : 'red'}>{active ? 'ACTIVE' : 'INACTIVE'}</Tag>
            ),
        },
        {
            title: 'Actions',
            key: 'actions',
            render: () => (
                <Space>
                    <Button size="small" icon={<EditOutlined />} />
                    <Button size="small" icon={<DeleteOutlined />} danger />
                </Space>
            ),
        },
    ];

    return (
        <Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
                <Title level={3} style={{ margin: 0 }}>User Management</Title>
                <Button type="primary" icon={<UserOutlined />}>Add User</Button>
            </div>

            <Table
                columns={columns}
                dataSource={users || []}
                loading={isLoading}
                rowKey="id"
            />
        </Card>
    );
}
