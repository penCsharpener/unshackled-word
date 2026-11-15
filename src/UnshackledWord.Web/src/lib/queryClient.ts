import { QueryClient } from "@tanstack/react-query";
import { isRateLimited } from "./api";

/** One retry for transient failures, but never for a 429: it would only spend more of the limiter budget. */
export const retryUnlessRateLimited = (failureCount: number, error: unknown) =>
  !isRateLimited(error) && failureCount < 1;

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      retry: retryUnlessRateLimited,
      refetchOnWindowFocus: false,
    },
  },
});
