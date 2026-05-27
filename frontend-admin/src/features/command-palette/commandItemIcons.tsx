import {
    CloudServerOutlined,
    DashboardOutlined,
    FileSearchOutlined,
    FileTextOutlined,
    KeyOutlined,
    LineChartOutlined,
    PlusOutlined,
    SafetyCertificateOutlined,
    ShopOutlined,
    TeamOutlined,
    ThunderboltOutlined,
    UserOutlined,
} from '@ant-design/icons';
import type { CommandItem, CommandItemType } from '@/features/command-palette/types';

export function defaultIconForCommandType(type: CommandItemType) {
    switch (type) {
        case 'user':
            return <UserOutlined />;
        case 'receipt':
            return <FileSearchOutlined />;
        case 'register':
            return <ShopOutlined />;
        case 'action':
            return <PlusOutlined />;
        default:
            return <ThunderboltOutlined />;
    }
}

export function iconForPinnedPage(id: string) {
    switch (id) {
        case 'page:dashboard':
            return <DashboardOutlined />;
        case 'page:users':
            return <TeamOutlined />;
        case 'page:registers':
            return <ShopOutlined />;
        case 'page:reports':
            return <LineChartOutlined />;
        case 'page:backup':
            return <CloudServerOutlined />;
        case 'page:audit':
            return <SafetyCertificateOutlined />;
        case 'page:license':
            return <KeyOutlined />;
        case 'page:receipts':
            return <FileSearchOutlined />;
        case 'page:report-center':
            return <FileTextOutlined />;
        default:
            return undefined;
    }
}

export function resolveCommandItemIcon(item: CommandItem) {
    return item.icon ?? iconForPinnedPage(item.id) ?? defaultIconForCommandType(item.type);
}
