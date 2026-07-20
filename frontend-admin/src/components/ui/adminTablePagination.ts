/** Shared Ant Design Table pagination defaults for FA catalog / list tables. */
export const ADMIN_TABLE_PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export const ADMIN_TABLE_DEFAULT_PAGE_SIZE = 20;

export const adminTablePaginationDefaults = {
    showSizeChanger: true,
    pageSizeOptions: [...ADMIN_TABLE_PAGE_SIZE_OPTIONS],
    hideOnSinglePage: false,
    pageSize: ADMIN_TABLE_DEFAULT_PAGE_SIZE,
} as const;
