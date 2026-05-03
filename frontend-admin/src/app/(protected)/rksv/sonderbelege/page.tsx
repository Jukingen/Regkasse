'use client';

import React, { Suspense } from 'react';
import { Spin } from 'antd';
import RksvSonderbelegePage from '@/features/rksv-operations/components/RksvSonderbelegePage';

function SonderbelegeFallback() {
    return (
        <div style={{ padding: 80, textAlign: 'center' }}>
            <Spin size="large" />
        </div>
    );
}

export default function RksvSonderbelegeRoutePage() {
    return (
        <Suspense fallback={<SonderbelegeFallback />}>
            <RksvSonderbelegePage />
        </Suspense>
    );
}
