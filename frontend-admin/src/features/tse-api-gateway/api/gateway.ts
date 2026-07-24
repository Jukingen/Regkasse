import { customInstance } from '@/lib/axios';

import type {
  ConfigureTseGateway,
  TseGatewayConfig,
  TseGatewayRequest,
  TseGatewayResponse,
  TseGatewayStatus,
} from '../types';

export async function getTseGatewayStatus(signal?: AbortSignal): Promise<TseGatewayStatus> {
  return customInstance<TseGatewayStatus>({
    url: '/api/admin/tse/api-gateway/status',
    method: 'GET',
    signal,
  });
}

export async function getTseGatewayConfig(signal?: AbortSignal): Promise<TseGatewayConfig> {
  return customInstance<TseGatewayConfig>({
    url: '/api/admin/tse/api-gateway/config',
    method: 'GET',
    signal,
  });
}

export async function configureTseGateway(body: ConfigureTseGateway): Promise<TseGatewayConfig> {
  return customInstance<TseGatewayConfig>({
    url: '/api/admin/tse/api-gateway/config',
    method: 'PUT',
    data: body,
  });
}

export async function routeTseGatewayRequest(
  body: TseGatewayRequest = { operation: 'HealthProbe' }
): Promise<TseGatewayResponse> {
  return customInstance<TseGatewayResponse>({
    url: '/api/admin/tse/api-gateway/route',
    method: 'POST',
    data: body,
  });
}
