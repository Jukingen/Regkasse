import { useUsersPolicy } from '@/shared/auth/usersPolicy';

/** Users screen permission helpers — thin wrapper over {@link useUsersPolicy}. */
export function useUserPermissions() {
  const policy = useUsersPolicy();

  return {
    canResetPassword: policy.canResetPassword,
    useGeneratedPasswordReset: policy.useGeneratedPasswordReset,
    canView: policy.canView,
    canCreate: policy.canCreate,
    canEdit: policy.canEdit,
    canDeactivate: policy.canDeactivate,
    canReactivate: policy.canReactivate,
  };
}
