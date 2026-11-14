import { Button } from './Button';

interface Props {
  page: number;
  pageSize: number;
  totalCount: number;
  onPage: (page: number) => void;
  disabled?: boolean;
}

export function Pagination({ page, pageSize, totalCount, onPage, disabled }: Props) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  return (
    <div className="pagination">
      <span className="muted">
        {totalCount} result{totalCount === 1 ? '' : 's'} · page {page} of {totalPages}
      </span>
      <Button variant="ghost" size="sm" disabled={disabled || page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </Button>
      <Button variant="ghost" size="sm" disabled={disabled || page >= totalPages} onClick={() => onPage(page + 1)}>
        Next
      </Button>
    </div>
  );
}
