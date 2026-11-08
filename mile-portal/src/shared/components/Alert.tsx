import type { ReactNode } from 'react';

interface Props {
  variant?: 'error' | 'success' | 'info' | 'warning';
  children: ReactNode;
  onClose?: () => void;
}

export function Alert({ variant = 'info', children, onClose }: Props) {
  return (
    <div className={`alert alert-${variant}`} role={variant === 'error' ? 'alert' : 'status'}>
      <div>{children}</div>
      {onClose && (
        <button className="alert-close" onClick={onClose} aria-label="Dismiss">
          ×
        </button>
      )}
    </div>
  );
}
