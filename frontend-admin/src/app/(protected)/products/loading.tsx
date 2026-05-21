import { Skeleton } from 'antd';

export default function ProductsRouteLoading() {
    return (
        <div style={{ padding: 4 }}>
            <Skeleton active title={{ width: '26%' }} paragraph={{ rows: 1 }} />
            <Skeleton active paragraph={{ rows: 14 }} style={{ marginTop: 24 }} />
        </div>
    );
}
