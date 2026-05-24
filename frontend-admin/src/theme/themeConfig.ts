import type { ThemeConfig } from 'antd';

const theme: ThemeConfig = {
    token: {
        fontSize: 14,
        colorPrimary: '#1677ff',
        borderRadius: 6,
        zIndexPopupBase: 1050,
    },
    components: {
        Layout: {
            bodyBg: '#f5f5f5',
            headerBg: '#ffffff',
        },
        Button: {
            defaultBg: 'transparent',
            defaultBorderColor: 'transparent',
        },
        Modal: {
            zIndexPopupBase: 1100,
        },
        Dropdown: {
            zIndexPopupBase: 1050,
        },
    },
};

export default theme;
