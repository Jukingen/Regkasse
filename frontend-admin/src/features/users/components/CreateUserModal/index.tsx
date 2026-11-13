'use client';

import { CreateUserModalContent } from './CreateUserModalContent';
import type { CreateUserModalProps } from './types';

export type {
  CreateUserFormValues,
  CreateUserModalProps,
  CreateUserQuickFormValues,
} from './types';

export function CreateUserModal(props: CreateUserModalProps) {
  if (!props.open) {
    return null;
  }
  return <CreateUserModalContent {...props} />;
}
