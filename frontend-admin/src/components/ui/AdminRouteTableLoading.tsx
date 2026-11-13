import { Skeleton } from 'antd';

export type AdminRouteTableLoadingProps = {
  titleWidth?: string | number;
  introRows?: number;
  tableRows?: number;
};

/** Shared route-level skeleton for admin list/form pages (used by `loading.tsx` files). */
export default function AdminRouteTableLoading({
  titleWidth = '28%',
  introRows = 2,
  tableRows = 14,
}: AdminRouteTableLoadingProps) {
  return (
    <div style={{ padding: 4 }}>
      <Skeleton active title={{ width: titleWidth }} paragraph={{ rows: 1 }} />
      {introRows > 0 ? (
        <Skeleton active paragraph={{ rows: introRows }} style={{ marginTop: 16 }} />
      ) : null}
      {tableRows > 0 ? (
        <Skeleton active paragraph={{ rows: tableRows }} style={{ marginTop: 24 }} />
      ) : null}
    </div>
  );
}
