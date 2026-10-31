export function Feedback({ error, message }: { error?: string; message?: string }) {
  return <>{error && <p className="error-message" role="alert">{error}</p>}{message && <p className="success-message" role="status">{message}</p>}</>
}
