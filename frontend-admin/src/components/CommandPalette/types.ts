import type { ReactNode } from 'react';

export type CommandItemType = 'page' | 'action' | 'user' | 'receipt' | 'register';

export type CommandItemGroup =
  'Navigation' | 'Actions' | 'Users' | 'Receipts' | 'Registers' | 'Recent';

export interface CommandItem {
  id: string;
  type: CommandItemType;
  label: string;
  description?: string;
  icon?: ReactNode;
  keywords: string[];
  action: () => void;
  group?: CommandItemGroup;
  dynamic?: boolean;
  menuKey?: string;
}
