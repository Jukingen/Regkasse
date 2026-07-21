import { jest } from '@jest/globals';

type MockAxiosResponse = { data: Record<string, unknown>; status: number };

// Mock axios instance
const mockAxios = {
  create: jest.fn(() => mockAxios),
  get: jest.fn<() => Promise<MockAxiosResponse>>(),
  post: jest.fn<() => Promise<MockAxiosResponse>>(),
  put: jest.fn<() => Promise<MockAxiosResponse>>(),
  delete: jest.fn<() => Promise<MockAxiosResponse>>(),
  patch: jest.fn<() => Promise<MockAxiosResponse>>(),
  interceptors: {
    request: {
      use: jest.fn(),
      eject: jest.fn(),
    },
    response: {
      use: jest.fn(),
      eject: jest.fn(),
    },
  },
  defaults: {
    baseURL: 'http://localhost:5184/api',
    timeout: 10000,
    headers: {
      'Content-Type': 'application/json',
    },
  },
};

// Default mock responses
mockAxios.get.mockResolvedValue({ data: {}, status: 200 });
mockAxios.post.mockResolvedValue({ data: {}, status: 200 });
mockAxios.put.mockResolvedValue({ data: {}, status: 200 });
mockAxios.delete.mockResolvedValue({ data: {}, status: 200 });
mockAxios.patch.mockResolvedValue({ data: {}, status: 200 });

export default mockAxios;
export { mockAxios as axios };
