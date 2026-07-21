import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { VirtualTable } from '@/components/VirtualTable';

describe('VirtualTable', () => {
  it('enables virtual mode and drops client pagination for 1000+ rows', () => {
    const rows = Array.from({ length: 1000 }, (_, i) => ({
      id: `row-${i}`,
      name: `Tenant ${i}`,
    }));

    const { container } = render(
      <VirtualTable
        rowKey="id"
        dataSource={rows}
        columns={[{ title: 'Name', dataIndex: 'name', key: 'name' }]}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        virtualizeFullList
        virtualScrollY={400}
        listItemHeight={54}
      />
    );

    // Client pagination footer should be gone when full-list virtualization is on.
    expect(container.querySelector('.ant-pagination')).toBeNull();

    // Virtual body mounts far fewer than 1000 row nodes (rc-virtual-list window).
    const rowNodes = container.querySelectorAll(
      '.ant-table-row, .ant-table-tbody-virtual .ant-table-row, [data-row-key]'
    );
    expect(rowNodes.length).toBeLessThan(200);
    expect(screen.getByText('Tenant 0')).toBeTruthy();
  });

  it('keeps server pagination when total is provided', () => {
    const rows = Array.from({ length: 50 }, (_, i) => ({
      id: `pay-${i}`,
      name: `Payment ${i}`,
    }));

    const onChange = vi.fn();
    const { container } = render(
      <VirtualTable
        rowKey="id"
        dataSource={rows}
        columns={[{ title: 'Name', dataIndex: 'name', key: 'name' }]}
        virtualizeFullList={false}
        pagination={{
          current: 1,
          pageSize: 50,
          total: 1000,
          onChange,
        }}
      />
    );

    expect(container.querySelector('.ant-pagination')).not.toBeNull();
  });
});
