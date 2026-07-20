'use client';

import { Card, Skeleton } from 'antd';

export function WidgetSkeleton() {
    return (
        <Card>
            <Skeleton active paragraph={{ rows: 3 }} />
            <div style={{ marginTop: 16 }}>
                <Skeleton.Button active block />
            </div>
        </Card>
    );
}
