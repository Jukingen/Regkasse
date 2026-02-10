'use client';

import React from 'react';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined, UserOutlined, ShoppingCartOutlined } from '@ant-design/icons';

const { Title, Paragraph } = Typography;

export default function DashboardPage() {
    return (
        <div style={{ padding: 24 }}>
            <Title level={2}>Dashboard</Title>
            <Paragraph>Welcome to the Regkasse Admin Panel.</Paragraph>

            <Row gutter={[16, 16]}>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} hoverable>
                        <Statistic
                            title="Today's Sales"
                            value={1128.93}
                            precision={2}
                            valueStyle={{ color: '#3f8600' }}
                            prefix={<ArrowUpOutlined />}
                            suffix="€"
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} hoverable>
                        <Statistic
                            title="Active Sessions"
                            value={4}
                            valueStyle={{ color: '#cf1322' }}
                            prefix={<UserOutlined />}
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} hoverable>
                        <Statistic
                            title="Total Invoices"
                            value={93}
                            prefix={<ShoppingCartOutlined />}
                            suffix=""
                        />
                    </Card>
                </Col>
                <Col xs={24} sm={12} md={6}>
                    <Card bordered={false} hoverable>
                        <Statistic
                            title="Performance"
                            value={98.5}
                            precision={1}
                            suffix="%"
                            valueStyle={{ color: '#3f8600' }}
                        />
                    </Card>
                </Col>
            </Row>

            <Row gutter={[16, 16]} style={{ marginTop: 24 }}>
                <Col span={24}>
                    <Card title="Quick Actions" bordered={false}>
                        <Paragraph>Content for quick actions will go here...</Paragraph>
                    </Card>
                </Col>
            </Row>
        </div>
    );
}
