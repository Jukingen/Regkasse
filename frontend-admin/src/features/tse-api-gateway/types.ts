export interface TseGatewayEndpoint {
  id: string;
  provider: string;
  endpoint: string;
  weight: number;
  enabled: boolean;
  sortOrder: number;
  status: 'healthy' | 'unhealthy' | 'unknown' | string;
  load: number;
  requests: number;
  successCount: number;
  failureCount: number;
  avgResponseTimeMs: number;
  lastCheckedAt?: string | null;
}

export interface TseGatewayConfig {
  strategy: 'RoundRobin' | 'LeastConnections' | 'Weighted' | string;
  endpoints: TseGatewayEndpoint[];
  healthCheckInterval: number;
  timeout: number;
  retryCount: number;
  enabled: boolean;
  updatedAt?: string | null;
}

export interface TseGatewayStats {
  totalRequests: number;
  successRate: number;
  avgResponseTime: number;
}

export interface TseGatewayStatus {
  enabled: boolean;
  strategy: string;
  stats: TseGatewayStats;
  endpoints: TseGatewayEndpoint[];
  generatedAt: string;
}

export interface TseGatewayRequest {
  operation?: string;
  preferredProvider?: string;
  correlationId?: string;
}

export interface TseGatewayResponse {
  success: boolean;
  operation: string;
  selectedProvider?: string | null;
  selectedEndpoint?: string | null;
  selectedEndpointId?: string | null;
  attempts: number;
  elapsedMs: number;
  message: string;
  correlationId?: string | null;
  simulationOnly: boolean;
}

export interface ConfigureTseGatewayEndpoint {
  id?: string;
  provider: string;
  endpoint: string;
  weight: number;
  enabled: boolean;
  sortOrder?: number;
}

export interface ConfigureTseGateway {
  strategy: string;
  endpoints: ConfigureTseGatewayEndpoint[];
  healthCheckInterval: number;
  timeout: number;
  retryCount: number;
  enabled: boolean;
}
