import { TenantDecommissionWizardPage } from '@/features/super-admin/components/TenantDecommissionWizardPage';

function DecommissionWizard() {
    return <TenantDecommissionWizardPage />;
}

export default function SuperAdminTenantDecommissionPage() {
    return <DecommissionWizard />;
}
