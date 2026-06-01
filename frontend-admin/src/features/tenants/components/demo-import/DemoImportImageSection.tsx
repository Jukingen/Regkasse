'use client';

import { Radio, Space, Typography } from 'antd';

import {
    DEFAULT_DEMO_IMPORT_IMAGE_MODE,
    type DemoImportImageMode,
} from './demoImportImage';

const { Text, Title } = Typography;

export type DemoImportImageSectionProps = {
    value: DemoImportImageMode;
    onChange: (value: DemoImportImageMode) => void;
};

export function DemoImportImageSection({ value, onChange }: DemoImportImageSectionProps) {
    return (
        <div
            style={{
                border: '1px solid #f0f0f0',
                borderRadius: 8,
                padding: 16,
                marginBottom: 16,
                background: '#fafafa',
            }}
        >
            <Title level={5} style={{ marginTop: 0, marginBottom: 12 }}>
                Produktbilder
            </Title>

            <Radio.Group value={value} onChange={(e) => onChange(e.target.value)}>
                <Space orientation="vertical" size="small">
                    <Radio value="categoryPlaceholder">
                        <Text>
                            Farbiges Platzhalterbild pro Kategorie{' '}
                            <Text type="secondary">(empfohlen, POS wirkt sofort nutzbar)</Text>
                        </Text>
                    </Radio>
                    <Radio value="defaultAsset">
                        <Text>Einheitliches Standard-Bild für alle Produkte</Text>
                    </Radio>
                    <Radio value="none">
                        <Text>Keine Bilder importieren</Text>
                        <Text type="secondary" style={{ display: 'block', fontSize: 12, marginLeft: 24 }}>
                            Schnellerer Import — Bilder später im Admin hochladen
                        </Text>
                    </Radio>
                </Space>
            </Radio.Group>

            {value === DEFAULT_DEMO_IMPORT_IMAGE_MODE ? (
                <Text type="secondary" style={{ display: 'block', marginTop: 10, fontSize: 12 }}>
                    Bilder werden lokal erzeugt (kein Unsplash/API-Key nötig) und wie Admin-Uploads gespeichert.
                </Text>
            ) : null}
        </div>
    );
}
