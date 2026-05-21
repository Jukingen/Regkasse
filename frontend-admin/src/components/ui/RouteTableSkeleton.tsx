import { Card, Skeleton } from 'antd';

type RouteTableSkeletonProps = {
    titleWidth?: string | number;
    filterRows?: number;
    tableRows?: number;
};

/** Shared route-level skeleton for list/report pages (RKSV, admin tables). */
export function RouteTableSkeleton({
    titleWidth = '30%',
    filterRows = 2,
    tableRows = 10,
}: RouteTableSkeletonProps) {
    return (
        <div style={{ padding: 4 }}>
            <Skeleton active title={{ width: titleWidth }} paragraph={{ rows: 1 }} />
            <Card size="small" style={{ marginTop: 16 }}>
                <Skeleton active paragraph={{ rows: filterRows }} />
            </Card>
            <Skeleton active paragraph={{ rows: tableRows }} style={{ marginTop: 24 }} />
        </div>
    );
}
