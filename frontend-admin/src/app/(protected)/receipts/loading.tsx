import { Skeleton } from 'antd';

export default function ReceiptsRouteLoading() {
    return (
        <div style={{ padding: 4 }}>
            <Skeleton active title={{ width: '30%' }} paragraph={{ rows: 1 }} />
            <Skeleton active paragraph={{ rows: 2 }} style={{ marginTop: 16 }} />
            <Skeleton active paragraph={{ rows: 12 }} style={{ marginTop: 24 }} />
        </div>
    );
}
