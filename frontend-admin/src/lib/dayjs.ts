/**
 * Shared Day.js instance with plugins required by Admin UI.
 * Import from `@/lib/dayjs` (or side-effect import this module once) so plugins are always registered.
 *
 * Plugins in use:
 * - utc — API timestamps / ISO week boundaries / license & reporting ranges
 * - timezone — depends on utc; available for explicit IANA conversion (not required by most call sites)
 * - relativeTime — “x minutes ago” (sessions, backup/offline widgets)
 * - isoWeek — ISO week labels on license dashboard charts
 * - customParseFormat / weekday / localeData / weekOfYear / weekYear / advancedFormat / dayOfYear —
 *   Ant Design DatePicker / RangePicker dayjs adapter recommendations
 *
 * No deprecated plugins are registered (dayjs 1.11.x has none in this set).
 *
 * Locales (de/en/tr) and `setDateLocale` live in `@/lib/dateUtils` — imported once from AppProviders
 * and kept in sync by I18nProvider when the Admin text language changes.
 */
import dayjs from 'dayjs';
import advancedFormat from 'dayjs/plugin/advancedFormat';
import customParseFormat from 'dayjs/plugin/customParseFormat';
import dayOfYear from 'dayjs/plugin/dayOfYear';
import isoWeek from 'dayjs/plugin/isoWeek';
import localeData from 'dayjs/plugin/localeData';
import relativeTime from 'dayjs/plugin/relativeTime';
import timezone from 'dayjs/plugin/timezone';
import utc from 'dayjs/plugin/utc';
import weekOfYear from 'dayjs/plugin/weekOfYear';
import weekYear from 'dayjs/plugin/weekYear';
import weekday from 'dayjs/plugin/weekday';

let pluginsExtended = false;

export function ensureDayjsPlugins(): void {
  if (pluginsExtended) return;
  dayjs.extend(utc);
  dayjs.extend(timezone);
  dayjs.extend(relativeTime);
  dayjs.extend(isoWeek);
  dayjs.extend(customParseFormat);
  dayjs.extend(advancedFormat);
  dayjs.extend(weekday);
  dayjs.extend(localeData);
  dayjs.extend(weekOfYear);
  dayjs.extend(weekYear);
  dayjs.extend(dayOfYear);
  pluginsExtended = true;
}

ensureDayjsPlugins();

export default dayjs;
export type { ConfigType, Dayjs } from 'dayjs';
export { dayjs };
