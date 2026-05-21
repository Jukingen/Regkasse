import { Card, Col, Row, Skeleton } from 'antd';

export default function DashboardRouteLoading() {
    return (
        <div style={{ padding: 4 }}>
            <Skeleton active title={{ width: '24%' }} paragraph={{ rows: 1 }} />
            <Skeleton active paragraph={{ rows: 2 }} style={{ marginTop: 16 }} />
            <Row gutter={16} style={{ marginTop: 24 }}>
                {Array.from({ length: 4 }).map((_, i) => (
                    <Col key={i} xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Skeleton active paragraph={{ rows: 2 }} />
                        </Card>
                    </Col>
                ))}
            </Row>
            <Skeleton active paragraph={{ rows: 8 }} style={{ marginTop: 24 }} />
        </div>
    );
}
