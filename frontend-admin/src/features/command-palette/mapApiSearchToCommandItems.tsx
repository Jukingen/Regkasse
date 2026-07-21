import { FileTextOutlined, UserOutlined } from '@ant-design/icons';

import type { UserInfo } from '@/api/generated/model';
import type { CommandItem } from '@/features/command-palette/types';
import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import type { AdminUserDto } from '@/features/users/api/users';

export function userDtoToCommandItem(
  row: AdminUserDto,
  openUser: (userId: string) => void,
  roleLabel?: string
): CommandItem {
  const name = [row.firstName, row.lastName].filter(Boolean).join(' ').trim();
  const label = row.userName?.trim() || row.email?.trim() || name || row.id || '—';
  const userId = row.id || '';
  const namePart = name || '—';
  const rolePart = roleLabel ?? row.role ?? '—';
  return {
    id: `user:${userId}`,
    type: 'user',
    label,
    description: `${namePart} • ${rolePart}`,
    icon: <UserOutlined />,
    group: 'Users',
    keywords: [userId, row.email ?? '', row.userName ?? '', label, namePart, rolePart],
    action: () => openUser(userId),
  };
}

export function tenantUserToCommandItem(
  user: UserInfo,
  openUser: (userId: string) => void,
  roleLabel?: string
): CommandItem {
  const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  const label = user.userName?.trim() || user.email?.trim() || name || user.id || '—';
  const namePart = name || '—';
  const rolePart = roleLabel ?? user.role ?? '—';
  const id = user.id ?? '';
  return {
    id: `user:${id}`,
    type: 'user',
    label,
    description: `${namePart} • ${rolePart}`,
    icon: <UserOutlined />,
    group: 'Users',
    keywords: [id, user.email ?? '', user.userName ?? '', label, namePart, rolePart],
    action: () => openUser(id),
  };
}

export function receiptRowToCommandItem(
  receipt: ReceiptListItemDto,
  openReceipt: (receiptId: string) => void,
  formatMoney: (value: number) => string,
  formatDate: (iso: string) => string
): CommandItem {
  const label = receipt.receiptNumber?.trim()
    ? `Beleg #${receipt.receiptNumber}`
    : receipt.receiptId;
  return {
    id: `receipt:${receipt.receiptId}`,
    type: 'receipt',
    label,
    description: `${formatMoney(receipt.grandTotal)} • ${formatDate(receipt.issuedAt)}`,
    icon: <FileTextOutlined />,
    group: 'Receipts',
    keywords: [receipt.receiptId, receipt.receiptNumber, receipt.cashRegisterId, label],
    action: () => openReceipt(receipt.receiptId),
  };
}
