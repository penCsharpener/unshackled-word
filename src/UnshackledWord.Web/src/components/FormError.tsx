/** Inline error / status message for forms; renders nothing when the message is empty. */
export function FormError({ message }: { message: string }) {
  if (!message) return null;
  return (
    <p role="alert" className="text-sm font-medium text-accent">
      {message}
    </p>
  );
}
