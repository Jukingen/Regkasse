import React, { ReactNode } from 'react';
import { AntdRegistry } from '@ant-design/nextjs-registry';
import { ConfigProvider } from 'antd';
import theme from '@/theme/themeConfig';
import QueryProvider from '@/app/providers';
import StyledComponentsRegistry from '@/lib/AntdRegistry';

import './globals.css';

export const metadata = {
    title: 'Regkasse Admin',
    description: 'Admin Panel for Regkasse POS',
};

export default function RootLayout({
    children,
}: {
    children: ReactNode;
}) {
    return (
        <html lang="en">
            <body style={{ margin: 0, padding: 0 }}>
                <QueryProvider>
                    <StyledComponentsRegistry>
                        <ConfigProvider theme={theme}>
                            {children}
                        </ConfigProvider>
                    </StyledComponentsRegistry>
                </QueryProvider>
            </body>
        </html>
    );
}
