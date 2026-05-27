export type BulkImportPreviewRow = {
    row: number;
    email: string;
    username?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    role: string;
    tenantSlug: string;
};

const REQUIRED_HEADERS = ['email', 'role', 'tenantslug'] as const;

function normalizeHeader(header: string): string {
    const h = header.trim().toLowerCase();
    if (h === 'tenant_slug') return 'tenantslug';
    if (h === 'first_name') return 'firstname';
    if (h === 'last_name') return 'lastname';
    if (h === 'user_name') return 'username';
    return h.replace(/\s+/g, '');
}

function parseCsvLine(line: string): string[] {
    const result: string[] = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < line.length; i++) {
        const c = line[i];
        if (c === '"') {
            if (inQuotes && line[i + 1] === '"') {
                current += '"';
                i++;
            } else {
                inQuotes = !inQuotes;
            }
            continue;
        }
        if (c === ',' && !inQuotes) {
            result.push(current);
            current = '';
            continue;
        }
        current += c;
    }
    result.push(current);
    return result;
}

/** Client-side CSV preview (first N data rows). Excel uses server preview API. */
export function parseCsvBulkImportPreview(
    text: string,
    maxRows: number,
): { totalRows: number; previewRows: BulkImportPreviewRow[]; parseError?: string } {
    const normalized = text.replace(/^\uFEFF/, '');
    const lines = normalized.split(/\r?\n/).filter((l) => l.trim().length > 0);
    if (lines.length === 0) {
        return { totalRows: 0, previewRows: [], parseError: 'File is empty.' };
    }

    const headers = parseCsvLine(lines[0]).map(normalizeHeader);
    const map = new Map<string, number>();
    headers.forEach((h, i) => {
        if (h && !map.has(h)) map.set(h, i);
    });

    for (const required of REQUIRED_HEADERS) {
        if (!map.has(required)) {
            return {
                totalRows: 0,
                previewRows: [],
                parseError: 'Missing required columns: email, role, tenantSlug.',
            };
        }
    }

    const get = (cells: string[], key: string) => {
        const idx = map.get(key);
        return idx != null && idx < cells.length ? cells[idx].trim() : '';
    };

    const dataRows: BulkImportPreviewRow[] = [];
    for (let i = 1; i < lines.length; i++) {
        const cells = parseCsvLine(lines[i]);
        dataRows.push({
            row: i + 1,
            email: get(cells, 'email'),
            username: get(cells, 'username') || null,
            firstName: get(cells, 'firstname') || null,
            lastName: get(cells, 'lastname') || null,
            role: get(cells, 'role'),
            tenantSlug: get(cells, 'tenantslug'),
        });
    }

    return {
        totalRows: dataRows.length,
        previewRows: dataRows.slice(0, maxRows),
    };
}

export function isCsvFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.csv') || file.type === 'text/csv';
}

export function isExcelFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.xlsx') || name.endsWith('.xls');
}
