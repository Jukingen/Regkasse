import { Skeleton } from 'antd';

/** Instant shell while a protected route segment loads (client pages + data). */
export default function ProtectedRouteLoading() {
  return (
    <div style={{ padding: 4 }}>
      <Skeleton active title={{ width: '32%' }} paragraph={false} />
      <Skeleton active paragraph={{ rows: 2 }} style={{ marginTop: 16 }} />
      <Skeleton active paragraph={{ rows: 10 }} style={{ marginTop: 24 }} />
    </div>
  );
}
