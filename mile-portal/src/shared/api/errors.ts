import axios from 'axios';
import type { ProblemDetails } from '@/shared/types/auth';

/** Turns an axios error (problem details, validation errors, network failure) into one readable line. */
export function getErrorMessage(err: unknown, fallback = 'Something went wrong.'): string {
  if (axios.isAxiosError(err)) {
    if (!err.response) return 'Cannot reach the server. Is the gateway (docker compose) running?';

    const { status } = err.response;
    const data = err.response.data as ProblemDetails | string | undefined;

    if (data && typeof data === 'object') {
      if (data.errors) {
        const messages = Object.values(data.errors).flat();
        if (messages.length) return messages.join(' ');
      }
      if (data.detail) return data.detail;
      if (data.title) return data.title;
    }

    if (status === 401) return 'You are not signed in or your session has expired.';
    if (status === 403) return 'You do not have permission to do that.';
    if (status === 502 || status === 503 || status === 504)
      return 'The backend service is unavailable. Check the auth-service container.';
    return `Request failed with status ${status}.`;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

export function getErrorStatus(err: unknown): number | undefined {
  return axios.isAxiosError(err) ? err.response?.status : undefined;
}
