export function Spinner({ size = 'md' }: { size?: 'sm' | 'md' }) {
  return <span className={`spinner spinner-${size}`} aria-label="Loading" />;
}

export function FullPageSpinner() {
  return (
    <div className="center-page">
      <Spinner />
    </div>
  );
}
