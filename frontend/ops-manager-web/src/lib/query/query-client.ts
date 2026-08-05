import { QueryClient } from "@tanstack/react-query";

import { ApiError } from "@/lib/api/errors";

export function shouldRetryQuery(
  failureCount: number,
  error: unknown,
): boolean {
  if (failureCount >= 2) return false;
  if (error instanceof ApiError && error.status < 500) return false;
  return true;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: shouldRetryQuery,
      },
      mutations: {
        retry: false,
      },
    },
  });
}
