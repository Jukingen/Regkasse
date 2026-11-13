import type { TablePaginationConfig } from 'antd/es/table';

/** Shared Ant Design Table pagination defaults for FA catalog / list tables. */
export const ADMIN_TABLE_PAGE_SIZE_OPTIONS: Array<string | number> = [10, 20, 50, 100];

export const ADMIN_TABLE_DEFAULT_PAGE_SIZE = 20;

export const adminTablePaginationDefaults: TablePaginationConfig = {
  showSizeChanger: true,
  pageSizeOptions: ADMIN_TABLE_PAGE_SIZE_OPTIONS,
  hideOnSinglePage: false,
  pageSize: ADMIN_TABLE_DEFAULT_PAGE_SIZE,
};
