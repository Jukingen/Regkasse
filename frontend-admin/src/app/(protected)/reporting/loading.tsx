import { Card, Skeleton } from 'antd';

export default function ReportingRouteLoading() {
  return (
    <div style={{ padding: 4 }}>
      <Skeleton active title={{ width: '32%' }} paragraph={{ rows: 1 }} />
      <Card size="small" style={{ marginTop: 16 }}>
        <Skeleton active paragraph={{ rows: 2 }} />
      </Card>
      <Skeleton active paragraph={{ rows: 10 }} style={{ marginTop: 24 }} />
    </div>
  );
}
