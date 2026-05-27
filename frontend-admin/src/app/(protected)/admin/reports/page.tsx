'use client';

import Link from 'next/link';
import { Card, Col, Row, Typography } from 'antd';
import { TeamOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { userActivityReportCopy as copy } from '@/features/reports/constants/copy';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const { Paragraph, Text } = Typography;

export default function AdminReportsHubPage() {
    const { t } = useI18n();
    return (
        <AdminPageShell>
            <AdminPageHeader
                title={copy.hubTitle}
                description={copy.hubIntro}
                breadcrumbs={[adminOverviewCrumb(t), { title: copy.hubTitle }]}
            />
            <Row gutter={[16, 16]}>
                <Col xs={24} sm={12} lg={8}>
                    <Link href="/admin/reports/user-activity" style={{ display: 'block' }}>
                        <Card hoverable>
                            <TeamOutlined style={{ fontSize: 28, color: '#1677ff' }} />
                            <Paragraph strong style={{ marginTop: 12, marginBottom: 4 }}>
                                {copy.openUserActivity}
                            </Paragraph>
                            <Text type="secondary">{copy.pageIntro}</Text>
                        </Card>
                    </Link>
                </Col>
            </Row>
        </AdminPageShell>
    );
}
