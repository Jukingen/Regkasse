/**
 * Provenance badges must expose lineage in accessible text (operators + AT).
 * Contract: copy lives in adminTruthBadges.tsx (German operator strings).
 */

import React from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';

describe('AdminTruthBadge semantic exposure (authoritative vs display vs derived)', () => {
    it('authoritative_api aria-label states API-sourced value and explicitly negates fiscal/legal finality', () => {
        render(<AdminTruthBadge kind="authoritative_api" />);
        const el = screen.getByLabelText(/Datenlage: API\./);
        expect(el.getAttribute('aria-label')).toContain('API-Feld');
        expect(el.getAttribute('aria-label')).toMatch(/keine Bewertung von Buchhaltung oder Rechtskonformität/i);
    });

    it('display_only_label aria-label warns against using label as sole machine FK for links', () => {
        render(<AdminTruthBadge kind="display_only_label" />);
        const label = screen.getByLabelText(/Datenlage: Anzeige\./).getAttribute('aria-label') ?? '';
        expect(label).toMatch(/nicht als alleiniger Maschinenbezug/i);
    });

    it('derived_from_foreign_row aria-label states foreign-row provenance', () => {
        render(<AdminTruthBadge kind="derived_from_foreign_row" />);
        expect(screen.getByLabelText(/Datenlage: Verknüpft\./).getAttribute('aria-label')).toMatch(
            /anderen API-Zeile/i,
        );
    });

    it('link_incomplete aria-label states deep links are not reliably possible', () => {
        render(<AdminTruthBadge kind="link_incomplete" />);
        expect(screen.getByLabelText(/Datenlage: Ohne Link\./).getAttribute('aria-label')).toMatch(
            /Deep-Links/i,
        );
    });

    it('diagnostic_support aria-label does not imply primary business truth', () => {
        render(<AdminTruthBadge kind="diagnostic_support" />);
        expect(screen.getByLabelText(/Datenlage: Diagnose\./).getAttribute('aria-label')).toMatch(
            /keine primäre Geschäfts/i,
        );
    });
});
