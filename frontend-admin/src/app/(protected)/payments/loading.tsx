import { Card, Skeleton } from 'antd';

export default function PaymentsRouteLoading() {
  return (
    <div style={{ padding: 4 }}>
      <Skeleton active title={{ width: '28%' }} paragraph={{ rows: 1 }} />
      <Card size="small" style={{ marginTop: 16 }}>
        <Skeleton active paragraph={{ rows: 1 }} />
      </Card>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))',
          gap: 16,
          marginTop: 16,
        }}
      >
        {Array.from({ length: 6 }).map((_, i) => (
          <Card key={i} size="small">
            <Skeleton active paragraph={{ rows: 2 }} />
          </Card>
        ))}
      </div>
      <Skeleton active paragraph={{ rows: 12 }} style={{ marginTop: 24 }} />
    </div>
  );
}
