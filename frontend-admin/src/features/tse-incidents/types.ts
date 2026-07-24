export type TseIncidentSeverity = 'Critical' | 'High' | 'Medium' | 'Low' | string;
export type TseIncidentStatus = 'Open' | 'Investigating' | 'Resolved' | 'Closed' | string;

export interface TseIncidentLog {
  id: string;
  eventType: string;
  message: string;
  actorUserId?: string | null;
  createdAt: string;
}

export interface TseIncidentAction {
  id: string;
  actionType: string;
  description: string;
  performedBy?: string | null;
  performedAt: string;
  isCompleted: boolean;
}

export interface TseIncident {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  deviceId?: string | null;
  deviceLabel?: string | null;
  title: string;
  description: string;
  severity: TseIncidentSeverity;
  status: TseIncidentStatus;
  detectedAt: string;
  resolvedAt?: string | null;
  resolution?: string | null;
  createdBy?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  logs: TseIncidentLog[];
  actions: TseIncidentAction[];
}

export interface CreateTseIncidentRequest {
  tenantId: string;
  deviceId?: string;
  title: string;
  description: string;
  severity: TseIncidentSeverity;
}

export interface UpdateTseIncidentStatusRequest {
  status: TseIncidentStatus;
  resolution?: string;
  note?: string;
}

export interface TseIncidentReport {
  incidentId: string;
  tenantId: string;
  tenantName?: string | null;
  deviceId?: string | null;
  deviceLabel?: string | null;
  title: string;
  severity: TseIncidentSeverity;
  status: TseIncidentStatus;
  detectedAt: string;
  resolvedAt?: string | null;
  timeToResolve?: string | null;
  summary: string;
  resolution?: string | null;
  logCount: number;
  actionCount: number;
  completedActionCount: number;
  generatedAt: string;
  timeline: TseIncidentLog[];
  actions: TseIncidentAction[];
}

export interface TseIncidentDashboard {
  openCount: number;
  investigatingCount: number;
  resolvedCount: number;
  closedCount: number;
  criticalOpenCount: number;
  incidents: TseIncident[];
}
