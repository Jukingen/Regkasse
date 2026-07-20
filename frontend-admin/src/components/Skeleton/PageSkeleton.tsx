'use client';

import { Col, Row } from 'antd';
import { WidgetSkeleton } from './WidgetSkeleton';

export type PageSkeletonProps = {
    /** Number of widget placeholders in the grid. */
    widgets?: number;
};

/** Dashboard / multi-card page loading grid. */
export function PageSkeleton({ widgets = 6 }: PageSkeletonProps) {
    return (
        <Row gutter={[16, 16]}>
            {Array.from({ length: Math.max(1, widgets) }).map((_, i) => (
                <Col key={i} xs={24} sm={12} lg={8}>
                    <WidgetSkeleton />
                </Col>
            ))}
        </Row>
    );
}
