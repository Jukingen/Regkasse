'use client';

import { useCallback, useMemo, useState } from 'react';
import { Button, Input, Modal, Select, Space, Typography, message } from 'antd';
import { DeleteOutlined, SaveOutlined } from '@ant-design/icons';

import type { DemoImportImageMode } from '@/features/tenants/components/demo-import/demoImportImage';
import type { DemoImportPriceAdjustmentState } from '@/features/tenants/components/demo-import/priceAdjustment';
import {
    BUILTIN_DEMO_IMPORT_PROFILES,
    applyDemoImportProfile,
    buildProfileFromWizardState,
    type AppliedDemoImportProfile,
    type DemoImportProfile,
} from '@/features/tenants/components/demo-import/importProfiles';
import {
    deleteDemoImportProfile,
    listSavedDemoImportProfiles,
    saveDemoImportProfile,
} from '@/features/tenants/components/demo-import/importProfileStorage';
import type { CategoryGroup } from '@/features/tenants/components/demo-import/categoryGroups';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';

const { Text } = Typography;

export type DemoImportProfileBarProps = {
    tenantId?: string;
    categoryGroups: CategoryGroup[];
    catalogProducts: CatalogProduct[];
    selectedGroupNames: Set<string>;
    selectedProductIds: Set<string>;
    priceAdjustment: DemoImportPriceAdjustmentState;
    imageMode: DemoImportImageMode;
    overwrite: boolean;
    onApply: (applied: AppliedDemoImportProfile) => void;
};

export function DemoImportProfileBar({
    tenantId,
    categoryGroups,
    catalogProducts,
    selectedGroupNames,
    selectedProductIds,
    priceAdjustment,
    imageMode,
    overwrite,
    onApply,
}: DemoImportProfileBarProps) {
    const [savedProfiles, setSavedProfiles] = useState<DemoImportProfile[]>(() =>
        listSavedDemoImportProfiles(tenantId),
    );
    const [selectedProfileId, setSelectedProfileId] = useState<string | undefined>();
    const [saveOpen, setSaveOpen] = useState(false);
    const [saveName, setSaveName] = useState('');
    const [saveDescription, setSaveDescription] = useState('');

    const allProfiles = useMemo(
        () => [...BUILTIN_DEMO_IMPORT_PROFILES, ...savedProfiles],
        [savedProfiles],
    );

    const selectOptions = useMemo(
        () => [
            {
                label: 'Vorlagen',
                options: BUILTIN_DEMO_IMPORT_PROFILES.map((p) => ({
                    value: p.id,
                    label: p.name,
                })),
            },
            ...(savedProfiles.length > 0
                ? [
                      {
                          label: 'Gespeichert',
                          options: savedProfiles.map((p) => ({
                              value: p.id,
                              label: p.name,
                          })),
                      },
                  ]
                : []),
        ],
        [savedProfiles],
    );

    const loadProfile = useCallback(
        (profile: DemoImportProfile) => {
            const applied = applyDemoImportProfile(profile, categoryGroups, catalogProducts);
            onApply(applied);
            setSelectedProfileId(profile.id);
            message.success(`Profil „${profile.name}" geladen`);
        },
        [catalogProducts, categoryGroups, onApply],
    );

    const handleLoadSelected = () => {
        const profile = allProfiles.find((p) => p.id === selectedProfileId);
        if (!profile) {
            message.warning('Bitte ein Profil auswählen');
            return;
        }
        loadProfile(profile);
    };

    const handleSave = () => {
        const name = saveName.trim();
        if (name.length < 2) {
            message.warning('Name muss mindestens 2 Zeichen haben');
            return;
        }
        if (selectedGroupNames.size === 0 && selectedProductIds.size === 0) {
            message.warning('Keine Auswahl zum Speichern — bitte Kategorien wählen');
            return;
        }

        const profile = buildProfileFromWizardState({
            name,
            description: saveDescription.trim() || undefined,
            groupNames: selectedGroupNames,
            selectedProductIds,
            priceAdjustment,
            imageMode,
            overwrite,
        });

        const next = saveDemoImportProfile(profile, tenantId);
        setSavedProfiles(next);
        setSelectedProfileId(profile.id);
        setSaveOpen(false);
        setSaveName('');
        setSaveDescription('');
        message.success(`Profil „${name}" gespeichert`);
    };

    const handleDeleteSaved = () => {
        const profile = savedProfiles.find((p) => p.id === selectedProfileId);
        if (!profile) return;

        Modal.confirm({
            title: 'Profil löschen?',
            content: `„${profile.name}" wird dauerhaft entfernt.`,
            okText: 'Löschen',
            okType: 'danger',
            cancelText: 'Abbrechen',
            onOk: () => {
                const next = deleteDemoImportProfile(profile.id, tenantId);
                setSavedProfiles(next);
                if (selectedProfileId === profile.id) setSelectedProfileId(undefined);
                message.success('Profil gelöscht');
            },
        });
    };

    const selectedIsSaved = savedProfiles.some((p) => p.id === selectedProfileId);

    return (
        <>
            <div
                style={{
                    padding: '12px 14px',
                    marginBottom: 12,
                    background: '#fafafa',
                    borderRadius: 8,
                    border: '1px solid #f0f0f0',
                }}
            >
                <Text strong style={{ display: 'block', marginBottom: 8 }}>
                    Import-Profil
                </Text>
                <Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 10 }}>
                    Schnellauswahl für ähnliche Betriebstypen (Restaurant, Kebap, Café, Pizzeria).
                </Text>
                <Space wrap style={{ width: '100%' }}>
                    <Select
                        placeholder="Profil wählen…"
                        style={{ minWidth: 220, flex: 1 }}
                        value={selectedProfileId}
                        onChange={setSelectedProfileId}
                        options={selectOptions}
                        optionRender={(option) => {
                            const profile = allProfiles.find((p) => p.id === option.value);
                            if (!profile) return option.label;
                            return (
                                <div>
                                    <div>{profile.name}</div>
                                    {profile.description ? (
                                        <Text type="secondary" style={{ fontSize: 11 }}>
                                            {profile.description}
                                        </Text>
                                    ) : null}
                                </div>
                            );
                        }}
                    />
                    <Button type="primary" onClick={handleLoadSelected}>
                        Laden
                    </Button>
                    <Button icon={<SaveOutlined />} onClick={() => setSaveOpen(true)}>
                        Speichern
                    </Button>
                    {selectedIsSaved ? (
                        <Button
                            danger
                            icon={<DeleteOutlined />}
                            onClick={handleDeleteSaved}
                        />
                    ) : null}
                </Space>
            </div>

            <Modal
                title="Import-Profil speichern"
                open={saveOpen}
                onCancel={() => setSaveOpen(false)}
                onOk={handleSave}
                okText="Speichern"
                cancelText="Abbrechen"
                destroyOnClose
            >
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    <div>
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            Name
                        </Text>
                        <Input
                            placeholder="z. B. Mein Lokal Standard"
                            value={saveName}
                            onChange={(e) => setSaveName(e.target.value)}
                            maxLength={80}
                        />
                    </div>
                    <div>
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            Beschreibung (optional)
                        </Text>
                        <Input.TextArea
                            placeholder="Kurznotiz zur Auswahl"
                            value={saveDescription}
                            onChange={(e) => setSaveDescription(e.target.value)}
                            rows={2}
                            maxLength={200}
                        />
                    </div>
                    <Text type="secondary" style={{ fontSize: 12 }}>
                        Gespeichert werden: {selectedGroupNames.size} Kategoriegruppe(n),{' '}
                        {selectedProductIds.size} Produkt(e), Preis- und Bild-Einstellungen.
                    </Text>
                </Space>
            </Modal>
        </>
    );
}
