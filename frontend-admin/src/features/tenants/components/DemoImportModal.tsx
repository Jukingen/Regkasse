'use client';

import { useEffect, useMemo, useState } from 'react';
import {
    Modal,
    Button,
    Checkbox,
    Alert,
    Space,
    Typography,
    Collapse,
    Divider,
    Tag,
    Spin,
    Badge,
    message,
} from 'antd';
import {
    ImportOutlined,
    CheckSquareOutlined,
    CheckCircleOutlined,
    DatabaseOutlined,
} from '@ant-design/icons';

import type { DemoProductImportResult } from '@/api/admin/products';
import {
    useDemoImportCatalog,
    useImportDemoProducts,
} from '@/features/products/api/productHooks';

const { Text } = Typography;
const { Panel } = Collapse;

export type DemoImportModalProps = {
    open: boolean;
    /** Super-admin tenant detail. Omit on products page (current tenant context). */
    tenantId?: string;
    tenantName: string;
    onClose: () => void;
    onSuccess: () => void;
};

type CategoryGroup = {
    name: string;
    displayName: string;
    description: string;
    icon: string;
    productCount: number;
    subcategories?: CategoryGroup[];
};

const CATEGORY_GROUPS: CategoryGroup[] = [
    {
        name: 'pizzas',
        displayName: '🍕 Pizzen',
        description: 'Margherita, Salami, Funghi, Hawaii, Capricciosa, Quattro Formaggi und mehr',
        icon: '🍕',
        productCount: 35,
        subcategories: [
            { name: 'Pizza-mittel', displayName: 'Mittel (Ø 36cm)', description: 'Klassische Pizzen', icon: '🍕', productCount: 25 },
            { name: 'Pizza-Partner', displayName: 'Partner (Ø 40cm)', description: 'Größere Pizzen zum Teilen', icon: '🍕', productCount: 25 },
            { name: 'Familien-Pizza', displayName: 'Familie (Ø 50cm)', description: 'Extra große Familien-Pizza', icon: '🍕', productCount: 1 },
            { name: 'Mexikanische-Pizza-mittel', displayName: 'Mexikanisch Mittel', description: 'Scharfe Pizzen mit Jalapenos', icon: '🌶️', productCount: 4 },
            { name: 'Mexikanische-Pizza-Partner', displayName: 'Mexikanisch Partner', description: 'Scharfe Pizzen zum Teilen', icon: '🌶️', productCount: 4 },
            { name: 'Calzone', displayName: 'Calzone', description: 'Gefaltete Pizzen', icon: '🥟', productCount: 11 },
        ],
    },
    {
        name: 'salads',
        displayName: '🥗 Salate',
        description: 'Frische Salate mit verschiedenen Dressings',
        icon: '🥗',
        productCount: 10,
        subcategories: [
            { name: 'Salate', displayName: 'Alle Salate', description: 'Chefsalat, Thunfischsalat, Bauernsalat', icon: '🥗', productCount: 10 },
        ],
    },
    {
        name: 'pasta',
        displayName: '🍝 Pasta',
        description: 'Bolognese, Carbonara, Arrabbiata, Lasagne',
        icon: '🍝',
        productCount: 6,
        subcategories: [
            { name: 'Pasta', displayName: 'Pastagerichte', description: 'Mit Nudelsorte nach Wahl', icon: '🍝', productCount: 6 },
        ],
    },
    {
        name: 'burgers',
        displayName: '🍔 Burger',
        description: 'Hamburger, Cheeseburger, Chickenburger, BBQ Burger',
        icon: '🍔',
        productCount: 9,
        subcategories: [
            { name: 'Burger', displayName: 'Burger', description: 'Mit Pommes frites serviert', icon: '🍔', productCount: 9 },
        ],
    },
    {
        name: 'kebap',
        displayName: '🥙 Kebap',
        description: 'Döner, Dürüm, Kebap-Teller',
        icon: '🥙',
        productCount: 10,
        subcategories: [
            { name: 'Kebap', displayName: 'Kebap Gerichte', description: 'Mit Salat, Tomaten, Zwiebeln, Rotkraut', icon: '🥙', productCount: 10 },
        ],
    },
    {
        name: 'snacks',
        displayName: '🥪 Stangerl & Baguettes',
        description: 'Stangerl, Baguettes, Imbiss',
        icon: '🥪',
        productCount: 25,
        subcategories: [
            { name: 'Stangerl', displayName: 'Stangerl', description: 'Mit Tomaten, Käse und Oregano', icon: '🥪', productCount: 11 },
            { name: 'Baguettes', displayName: 'Baguettes', description: 'Mit Tomaten, Käse und Oregano', icon: '🥖', productCount: 10 },
            { name: 'Imbiss', displayName: 'Imbiss', description: 'Schnitzel, Nuggets, Wings, Pommes', icon: '🍟', productCount: 11 },
        ],
    },
    {
        name: 'desserts',
        displayName: '🍰 Desserts',
        description: 'Mohr im Hemd, Palatschinken',
        icon: '🍰',
        productCount: 3,
        subcategories: [
            { name: 'Desserts', displayName: 'Desserts', description: 'Süße Nachspeisen', icon: '🍰', productCount: 3 },
        ],
    },
    {
        name: 'drinks',
        displayName: '🥤 Getränke',
        description: 'Coca Cola, Fanta, Sprite, Mezzo Mix, Almdudler, Eistee, Ayran, Mineralwasser, Red Bull',
        icon: '🥤',
        productCount: 16,
        subcategories: [
            { name: 'Alkoholfreie-Getrnke', displayName: 'Alkoholfreie Getränke', description: '0,33l und 0,5l Flaschen', icon: '🥤', productCount: 16 },
        ],
    },
];

function enrichGroupsWithCatalog(groups: CategoryGroup[], countByName: Map<string, number>): CategoryGroup[] {
    return groups.map((group) => {
        const subcategories = group.subcategories?.map((sub) => ({
            ...sub,
            productCount: countByName.get(sub.name) ?? sub.productCount,
        }));
        const productCount = subcategories
            ? subcategories.reduce((sum, sub) => sum + sub.productCount, 0)
            : countByName.get(group.name) ?? group.productCount;

        return { ...group, subcategories, productCount };
    });
}

function getSelectedCategoryNames(groups: CategoryGroup[], selectedGroups: string[]): string[] {
    const categories: string[] = [];

    for (const group of groups) {
        if (!selectedGroups.includes(group.name)) continue;

        if (group.subcategories?.length) {
            categories.push(...group.subcategories.map((sub) => sub.name));
        } else {
            categories.push(group.name);
        }
    }

    return categories;
}

function extractImportErrorMessage(error: unknown): string {
    if (error && typeof error === 'object' && 'response' in error) {
        const data = (error as { response?: { data?: { message?: string; error?: string; errorMessage?: string } } })
            .response?.data;
        return data?.message ?? data?.error ?? data?.errorMessage ?? 'Fehler beim Importieren der Demo-Produkte';
    }
    return 'Fehler beim Importieren der Demo-Produkte';
}

function formatImportSuccessContent(result: DemoProductImportResult) {
    return (
        <div>
            <p>
                <CheckCircleOutlined style={{ color: '#52c41a' }} /> {result.created} Produkte neu erstellt
            </p>
            <p>
                <CheckCircleOutlined style={{ color: '#1677ff' }} /> {result.updated} Produkte aktualisiert
            </p>
            <p>{result.skipped} Produkte übersprungen</p>
            <Divider />
            <p>
                <DatabaseOutlined /> {result.totalProductCount ?? 0} Produkte in{' '}
                {result.selectedCategoryCount ?? 0} Kategorien
            </p>
            {result.categorySummaries?.map((cat) => (
                <div key={cat.categoryName} style={{ fontSize: 12, color: '#595959' }}>
                    • {cat.categoryName}: {cat.created} neu, {cat.skipped} übersprungen
                </div>
            ))}
        </div>
    );
}

export function DemoImportModal({ open, tenantId, tenantName, onClose, onSuccess }: DemoImportModalProps) {
    const [selectedGroups, setSelectedGroups] = useState<string[]>([]);
    const [expandedGroups, setExpandedGroups] = useState<string[]>(['pizzas', 'salads']);
    const [overwrite, setOverwrite] = useState(false);

    const importDemo = useImportDemoProducts();
    const catalogQuery = useDemoImportCatalog(open);

    const categoryGroups = useMemo(() => {
        const countByName = new Map(
            catalogQuery.data?.categories.map((category) => [category.name, category.productCount]) ?? [],
        );
        return enrichGroupsWithCatalog(CATEGORY_GROUPS, countByName);
    }, [catalogQuery.data]);

    const selectAll = selectedGroups.length === categoryGroups.length && categoryGroups.length > 0;
    const indeterminate = selectedGroups.length > 0 && !selectAll;

    useEffect(() => {
        if (!open) return;
        setSelectedGroups([]);
        setExpandedGroups(['pizzas', 'salads']);
        setOverwrite(false);
    }, [open]);

    const handleSelectAll = (checked: boolean) => {
        setSelectedGroups(checked ? categoryGroups.map((group) => group.name) : []);
    };

    const handleGroupSelect = (groupName: string, checked: boolean) => {
        setSelectedGroups((prev) =>
            checked ? [...prev, groupName] : prev.filter((name) => name !== groupName),
        );
    };

    const handleImport = async () => {
        const selectedCategories = getSelectedCategoryNames(categoryGroups, selectedGroups);

        if (selectedCategories.length === 0) {
            message.warning('Bitte wählen Sie mindestens eine Kategorie aus.');
            return;
        }

        try {
            const result = await importDemo.mutateAsync({
                tenantId,
                selectedCategories,
                overwriteExisting: overwrite,
            });

            Modal.success({
                title: 'Demo Produkte importiert',
                width: 500,
                content: formatImportSuccessContent(result),
                onOk: () => {
                    onClose();
                    onSuccess();
                },
            });
        } catch (error) {
            message.error(extractImportErrorMessage(error));
        }
    };

    const totalSelectedProducts = categoryGroups
        .filter((group) => selectedGroups.includes(group.name))
        .reduce((sum, group) => sum + group.productCount, 0);

    return (
        <Modal
            title={
                <Space>
                    <ImportOutlined />
                    <span>Demo Produkte importieren</span>
                </Space>
            }
            open={open}
            onCancel={onClose}
            width={700}
            footer={[
                <Button key="cancel" onClick={onClose}>
                    Abbrechen
                </Button>,
                <Button
                    key="import"
                    type="primary"
                    onClick={handleImport}
                    loading={importDemo.isPending}
                    disabled={selectedGroups.length === 0 || catalogQuery.isLoading}
                >
                    {selectedGroups.length === 0
                        ? 'Kategorien auswählen'
                        : `${totalSelectedProducts} Produkte importieren`}
                </Button>,
            ]}
        >
            <Alert
                message={`Produkte für "${tenantName}"`}
                description="Wählen Sie die Kategorien aus, die Sie importieren möchten. Sie können die Produkte später bearbeiten oder löschen."
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
            />

            {catalogQuery.isLoading ? (
                <div style={{ textAlign: 'center', padding: 24 }}>
                    <Spin />
                </div>
            ) : catalogQuery.isError ? (
                <Alert
                    type="error"
                    showIcon
                    message="Katalog konnte nicht geladen werden"
                    style={{ marginBottom: 16 }}
                />
            ) : (
                <>
                    <div
                        style={{
                            marginBottom: 16,
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                        }}
                    >
                        <Space>
                            <Button
                                size="small"
                                icon={<CheckSquareOutlined />}
                                onClick={() => handleSelectAll(!selectAll)}
                            >
                                {selectAll ? 'Alle abwählen' : 'Alle auswählen'}
                            </Button>
                            <Badge count={selectedGroups.length} showZero>
                                <Text type="secondary">Kategorien ausgewählt</Text>
                            </Badge>
                        </Space>
                        <Text type="secondary">
                            <DatabaseOutlined /> {totalSelectedProducts} Produkte
                        </Text>
                    </div>

                    <Divider style={{ margin: '8px 0' }} />

                    <div style={{ maxHeight: 400, overflow: 'auto' }}>
                        <Checkbox
                            checked={selectAll}
                            indeterminate={indeterminate}
                            onChange={(e) => handleSelectAll(e.target.checked)}
                            style={{ marginBottom: 12, fontWeight: 'bold' }}
                        >
                            <Text strong>Alle Kategorien</Text>
                        </Checkbox>

                        <Collapse
                            activeKey={expandedGroups}
                            onChange={(keys) => setExpandedGroups(keys as string[])}
                            ghost
                        >
                            {categoryGroups.map((group) => (
                                <Panel
                                    key={group.name}
                                    header={
                                        <div
                                            style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                width: '100%',
                                            }}
                                        >
                                            <Checkbox
                                                checked={selectedGroups.includes(group.name)}
                                                onChange={(e) => handleGroupSelect(group.name, e.target.checked)}
                                                onClick={(e) => e.stopPropagation()}
                                            >
                                                <Space>
                                                    <span style={{ fontSize: 20 }}>{group.icon}</span>
                                                    <Text strong>{group.displayName}</Text>
                                                    <Tag color="blue">{group.productCount} Produkte</Tag>
                                                </Space>
                                            </Checkbox>
                                        </div>
                                    }
                                >
                                    <div style={{ paddingLeft: 32 }}>
                                        <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                                            {group.description}
                                        </Text>

                                        {group.subcategories ? (
                                            <div style={{ paddingLeft: 16, marginTop: 8 }}>
                                                {group.subcategories.map((sub) => (
                                                    <div
                                                        key={sub.name}
                                                        style={{
                                                            display: 'flex',
                                                            justifyContent: 'space-between',
                                                            alignItems: 'center',
                                                            padding: '4px 0',
                                                        }}
                                                    >
                                                        <Space>
                                                            <span>{sub.icon}</span>
                                                            <Text>{sub.displayName}</Text>
                                                            <Text type="secondary" style={{ fontSize: 12 }}>
                                                                {sub.description}
                                                            </Text>
                                                        </Space>
                                                        <Tag>{sub.productCount} Produkte</Tag>
                                                    </div>
                                                ))}
                                            </div>
                                        ) : null}
                                    </div>
                                </Panel>
                            ))}
                        </Collapse>
                    </div>
                </>
            )}

            <Divider />

            <div
                style={{
                    background: '#fffbe6',
                    padding: 12,
                    borderRadius: 6,
                    marginBottom: 16,
                }}
            >
                <Checkbox checked={overwrite} onChange={(e) => setOverwrite(e.target.checked)}>
                    <Text>Vorhandene Produkte überschreiben (Gleicher Name)</Text>
                </Checkbox>
                <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 4, marginLeft: 24 }}>
                    Aktivieren Sie diese Option, um bestehende Produkte mit den Demo-Daten zu aktualisieren.
                </div>
            </div>

            <Alert
                message="Hinweis"
                description="Dieser Vorgang kann je nach Anzahl der ausgewählten Produkte einige Sekunden dauern."
                type="warning"
                showIcon
            />
        </Modal>
    );
}
