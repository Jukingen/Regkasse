import { useEffect, useMemo } from 'react';
import { Form } from 'antd';
import type { FormInstance } from 'antd';
import { useQuery } from '@tanstack/react-query';

import { useDebounce } from '@/hooks/useDebounce';
import { useI18n } from '@/i18n';
import { getTenantAppBaseDomain } from '@/lib/auth/impersonationHandoff';
import {
    buildTenantPortalUrl,
    checkAdminTenantSlugAvailability,
} from '@/features/super-admin/api/adminTenants';
import type { CreateTenantFormValues } from '@/features/super-admin/components/CreateTenantModal';
import {
    validateAddress,
    validateCompanyName,
    validateContactEmail,
    validatePhone,
} from '@/features/super-admin/lib/tenantCreateValidation';
import {
    normalizeTenantSlugInput,
    sanitizeTenantSlugKeystroke,
    suggestTenantSlugFromName,
    validateTenantSlug,
    type TenantSlugValidationCode,
} from '@/features/super-admin/lib/tenantSlug';

export type SlugAvailabilityUi = 'idle' | 'checking' | 'available' | 'taken';

function slugValidationMessage(t: (key: string) => string, code: TenantSlugValidationCode): string {
    return t(`tenants.create.fields.slug.errors.${code}`);
}

export function useTenantCreateFormFields(
    form: FormInstance<CreateTenantFormValues & { formError?: string }>,
    open: boolean,
) {
    const { t } = useI18n();
    const baseDomain = getTenantAppBaseDomain();

    const nameWatch = Form.useWatch('name', form);
    const slugWatch = Form.useWatch('slug', form);
    const emailWatch = Form.useWatch('email', form);
    const phoneWatch = Form.useWatch('phone', form);
    const addressWatch = Form.useWatch('address', form);

    const debouncedSlug = useDebounce(slugWatch ?? '', 400);
    const normalizedSlug = useMemo(() => normalizeTenantSlugInput(debouncedSlug), [debouncedSlug]);

    const slugFormatError = useMemo(() => {
        if (!slugWatch) {
            return null;
        }
        return validateTenantSlug(slugWatch, { inProgress: true });
    }, [slugWatch]);

    const slugReadyForAvailability = !slugFormatError && normalizedSlug.length > 0;

    const availabilityQuery = useQuery({
        queryKey: ['admin', 'tenants', 'slug-availability', normalizedSlug],
        queryFn: () => checkAdminTenantSlugAvailability(normalizedSlug),
        enabled: open && slugReadyForAvailability,
        staleTime: 30_000,
        retry: false,
    });

    const slugAvailabilityUi: SlugAvailabilityUi = useMemo(() => {
        if (!slugReadyForAvailability) {
            return 'idle';
        }
        if (availabilityQuery.isFetching) {
            return 'checking';
        }
        if (availabilityQuery.data?.available) {
            return 'available';
        }
        if (availabilityQuery.data && !availabilityQuery.data.available) {
            return 'taken';
        }
        return 'idle';
    }, [slugReadyForAvailability, availabilityQuery.isFetching, availabilityQuery.data]);

    const portalPreviewUrl = useMemo(() => {
        if (slugAvailabilityUi !== 'available') {
            return null;
        }
        return buildTenantPortalUrl(normalizedSlug);
    }, [slugAvailabilityUi, normalizedSlug]);

    useEffect(() => {
        if (slugReadyForAvailability) {
            void form.validateFields(['slug']);
        }
    }, [availabilityQuery.data, availabilityQuery.isFetching, slugReadyForAvailability, form]);

    const slugFieldStatus = useMemo(() => {
        if (!slugWatch) {
            return undefined;
        }
        if (slugFormatError) {
            return 'error' as const;
        }
        if (availabilityQuery.isFetching) {
            return 'validating' as const;
        }
        if (availabilityQuery.data?.available) {
            return 'success' as const;
        }
        if (availabilityQuery.data && !availabilityQuery.data.available) {
            return 'error' as const;
        }
        return undefined;
    }, [slugWatch, slugFormatError, availabilityQuery.isFetching, availabilityQuery.data]);

    const nameFieldStatus = useMemo(() => {
        if (!nameWatch?.trim()) {
            return undefined;
        }
        return validateCompanyName(nameWatch) ? ('error' as const) : ('success' as const);
    }, [nameWatch]);

    const emailFieldStatus = useMemo(() => {
        if (!emailWatch?.trim()) {
            return undefined;
        }
        return validateContactEmail(emailWatch) ? ('error' as const) : ('success' as const);
    }, [emailWatch]);

    const phoneFieldStatus = useMemo(() => {
        if (!phoneWatch?.trim()) {
            return undefined;
        }
        return validatePhone(phoneWatch) ? ('error' as const) : ('success' as const);
    }, [phoneWatch]);

    const addressFieldStatus = useMemo(() => {
        if (!addressWatch?.trim()) {
            return undefined;
        }
        return validateAddress(addressWatch) ? ('error' as const) : ('success' as const);
    }, [addressWatch]);

    const slugRules = useMemo(
        () => [
            { required: true, message: t('tenants.create.fields.slug.errors.required') },
            {
                pattern: /^[a-z0-9]+(?:-[a-z0-9]+)*$/,
                message: t('tenants.create.fields.slug.errors.pattern'),
                validateTrigger: ['onChange', 'onBlur'],
            },
            {
                validateTrigger: ['onChange', 'onBlur'],
                validator: async (_: unknown, value: string | undefined) => {
                    const inProgressCode = validateTenantSlug(value ?? '', { inProgress: true });
                    if (inProgressCode && inProgressCode !== 'required') {
                        throw new Error(slugValidationMessage(t, inProgressCode));
                    }
                    const finalCode = validateTenantSlug(value ?? '');
                    if (finalCode) {
                        throw new Error(slugValidationMessage(t, finalCode));
                    }
                    const slug = normalizeTenantSlugInput(value ?? '');
                    if (!slug) {
                        throw new Error(slugValidationMessage(t, 'required'));
                    }
                    if (availabilityQuery.isFetching) {
                        throw new Error(slugValidationMessage(t, 'checking'));
                    }
                    if (availabilityQuery.isError) {
                        throw new Error(t('tenants.create.fields.slug.errors.availabilityFailed'));
                    }
                    const availability = availabilityQuery.data;
                    if (!availability || availability.normalizedSlug !== slug) {
                        throw new Error(slugValidationMessage(t, 'checking'));
                    }
                    if (!availability.isValid) {
                        throw new Error(slugValidationMessage(t, 'invalid'));
                    }
                    if (!availability.available) {
                        throw new Error(slugValidationMessage(t, 'taken'));
                    }
                },
            },
        ],
        [t, availabilityQuery.data, availabilityQuery.isFetching, availabilityQuery.isError],
    );

    const nameRules = useMemo(
        () => [
            {
                validateTrigger: ['onChange', 'onBlur'] as const,
                validator: async (_: unknown, value: string | undefined) => {
                    const code = validateCompanyName(value);
                    if (code) {
                        throw new Error(t(`tenants.create.fields.name.errors.${code}`));
                    }
                },
            },
        ],
        [t],
    );

    const emailRules = useMemo(
        () => [
            { required: true, message: t('tenants.create.fields.contactEmail.errors.required') },
            {
                type: 'email' as const,
                message: t('tenants.create.fields.contactEmail.errors.invalid'),
                validateTrigger: ['onChange', 'onBlur'],
            },
            {
                validateTrigger: ['onChange', 'onBlur'] as const,
                validator: async (_: unknown, value: string | undefined) => {
                    const code = validateContactEmail(value);
                    if (code) {
                        throw new Error(t(`tenants.create.fields.contactEmail.errors.${code}`));
                    }
                },
            },
        ],
        [t],
    );

    const phoneRules = useMemo(
        () => [
            {
                validateTrigger: ['onChange', 'onBlur'] as const,
                validator: async (_: unknown, value: string | undefined) => {
                    const code = validatePhone(value);
                    if (code) {
                        throw new Error(t(`tenants.create.fields.phone.errors.${code}`));
                    }
                },
            },
        ],
        [t],
    );

    const addressRules = useMemo(
        () => [
            {
                validateTrigger: ['onChange', 'onBlur'] as const,
                validator: async (_: unknown, value: string | undefined) => {
                    const code = validateAddress(value);
                    if (code) {
                        throw new Error(t(`tenants.create.fields.address.errors.${code}`));
                    }
                },
            },
        ],
        [t],
    );

    const handleSlugChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        form.setFieldValue('slug', sanitizeTenantSlugKeystroke(event.target.value));
        void form.validateFields(['slug']);
    };

    const handleSlugBlur = () => {
        const raw = form.getFieldValue('slug');
        if (!raw) {
            return;
        }
        const finalized = normalizeTenantSlugInput(raw);
        if (finalized !== raw) {
            form.setFieldValue('slug', finalized);
        }
        void form.validateFields(['slug']);
    };

    const handleNameBlur = () => {
        const name = form.getFieldValue('name');
        const slug = form.getFieldValue('slug');
        if (name && !slug) {
            form.setFieldValue('slug', suggestTenantSlugFromName(name));
            void form.validateFields(['slug']);
        }
    };

    const canSubmit = Boolean(
        nameWatch?.trim() &&
            !validateCompanyName(nameWatch) &&
            emailWatch?.trim() &&
            !validateContactEmail(emailWatch) &&
            slugReadyForAvailability &&
            slugAvailabilityUi === 'available' &&
            !slugFormatError &&
            !availabilityQuery.isFetching,
    );

    return {
        t,
        baseDomain,
        slugWatch,
        slugRules,
        slugFieldStatus,
        slugAvailabilityUi,
        portalPreviewUrl,
        nameRules,
        nameFieldStatus,
        emailRules,
        emailFieldStatus,
        phoneRules,
        phoneFieldStatus,
        addressRules,
        addressFieldStatus,
        handleSlugChange,
        handleSlugBlur,
        handleNameBlur,
        canSubmit,
    };
}
