import { FORMAT_EMPTY_DISPLAY } from '@/i18n';

export function formatRelativeTime(input: string | null | undefined, formatLocale: string): string {
    if (!input) {
        return FORMAT_EMPTY_DISPLAY;
    }

    const date = new Date(input);
    if (Number.isNaN(date.getTime())) {
        return FORMAT_EMPTY_DISPLAY;
    }

    const diffMs = date.getTime() - Date.now();
    const diffSeconds = Math.round(diffMs / 1000);
    const absSeconds = Math.abs(diffSeconds);
    const rtf = new Intl.RelativeTimeFormat(formatLocale, { numeric: 'auto' });

    if (absSeconds < 60) {
        return rtf.format(diffSeconds, 'second');
    }

    const diffMinutes = Math.round(diffSeconds / 60);
    if (Math.abs(diffMinutes) < 60) {
        return rtf.format(diffMinutes, 'minute');
    }

    const diffHours = Math.round(diffMinutes / 60);
    if (Math.abs(diffHours) < 24) {
        return rtf.format(diffHours, 'hour');
    }

    const diffDays = Math.round(diffHours / 24);
    if (Math.abs(diffDays) < 30) {
        return rtf.format(diffDays, 'day');
    }

    const diffMonths = Math.round(diffDays / 30);
    if (Math.abs(diffMonths) < 12) {
        return rtf.format(diffMonths, 'month');
    }

    const diffYears = Math.round(diffDays / 365);
    return rtf.format(diffYears, 'year');
}
