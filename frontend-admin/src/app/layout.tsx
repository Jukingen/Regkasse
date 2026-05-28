import React, { ReactNode } from 'react';
import { AppProviders } from '@/providers/AppProviders';
import StyledComponentsRegistry from '@/lib/AntdRegistry';
import { THEME_BOOTSTRAP_SCRIPT } from '@/lib/personalization/themeBootstrapScript';

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
        <html lang="de" suppressHydrationWarning>
            <head>
                <script dangerouslySetInnerHTML={{ __html: THEME_BOOTSTRAP_SCRIPT }} />
            </head>
            <body style={{ margin: 0, padding: 0 }} suppressHydrationWarning>
                <AppProviders>
                    <StyledComponentsRegistry>{children}</StyledComponentsRegistry>
                </AppProviders>
            </body>
        </html>
    );
}
