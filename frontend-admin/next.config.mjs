/** @type {import('next').NextConfig} */
const nextConfig = {
    transpilePackages: ['@ant-design/icons', 'antd', 'rc-util', 'rc-pagination', 'rc-picker', 'rc-notification', 'rc-tooltip'],
    reactStrictMode: true,
};

export default nextConfig;
