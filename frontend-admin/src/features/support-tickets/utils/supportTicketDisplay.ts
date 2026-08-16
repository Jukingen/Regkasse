import type { AdminTranslationKey } from '@/i18n/translationKey';

export function supportStatusLabelKey(status: string): AdminTranslationKey {
  switch (status) {
    case 'InProgress':
      return 'support.tickets.statusInProgress';
    case 'WaitingOnTenant':
      return 'support.tickets.statusWaitingOnTenant';
    case 'WaitingOnStaff':
      return 'support.tickets.statusWaitingOnStaff';
    case 'Resolved':
      return 'support.tickets.statusResolved';
    case 'Closed':
      return 'support.tickets.statusClosed';
    default:
      return 'support.tickets.statusOpen';
  }
}

export function supportStatusColor(status: string): string {
  switch (status) {
    case 'InProgress':
    case 'WaitingOnTenant':
    case 'WaitingOnStaff':
      return 'gold';
    case 'Resolved':
      return 'green';
    case 'Closed':
      return 'default';
    default:
      return 'blue';
  }
}

export function supportCategoryLabelKey(category: string): AdminTranslationKey {
  switch (category) {
    case 'Billing':
      return 'support.tickets.categoryBilling';
    case 'License':
      return 'support.tickets.categoryLicense';
    case 'FeatureRequest':
      return 'support.tickets.categoryFeature';
    case 'General':
      return 'support.tickets.categoryGeneral';
    default:
      return 'support.tickets.categoryTechnical';
  }
}

export function supportPriorityLabelKey(priority: string): AdminTranslationKey {
  if (priority === 'Low') return 'support.tickets.priorityLow';
  if (priority === 'High') return 'support.tickets.priorityHigh';
  if (priority === 'Urgent') return 'support.tickets.priorityUrgent';
  return 'support.tickets.priorityMedium';
}

export function supportPriorityColor(priority: string): string {
  if (priority === 'Urgent') return 'magenta';
  if (priority === 'High') return 'red';
  if (priority === 'Low') return 'default';
  return 'gold';
}
