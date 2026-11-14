import type { ReactNode } from 'react';
import { getErrorMessage } from '@/shared/api';
import { Alert } from './Alert';
import { Spinner } from './Spinner';

interface Props {
  isLoading: boolean;
  error: unknown;
  children: ReactNode;
  /** Message for this section when its service is down (NFR-3: degrade, don't break). */
  errorPrefix?: string;
}

/** Loading spinner / error alert / content, for one query-backed section. */
export function QueryState({ isLoading, error, children, errorPrefix }: Props) {
  if (isLoading)
    return (
      <div className="center-block">
        <Spinner />
      </div>
    );
  if (error) return <Alert variant="error">{errorPrefix ? `${errorPrefix}: ${getErrorMessage(error)}` : getErrorMessage(error)}</Alert>;
  return <>{children}</>;
}
