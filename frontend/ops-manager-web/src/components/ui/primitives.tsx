"use client";

import {
  CSSProperties,
  forwardRef,
  useEffect,
  useId,
  useRef,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Inbox,
  LoaderCircle,
  X,
} from "lucide-react";

import { cn } from "@/lib/utils";

export const Button = forwardRef<
  HTMLButtonElement,
  ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: "primary" | "secondary" | "ghost" | "danger";
    size?: "sm" | "md";
    busy?: boolean;
  }
>(function Button(
  {
    className,
    variant = "primary",
    size = "md",
    busy,
    disabled,
    children,
    ...props
  },
  ref,
) {
  return (
    <button
      ref={ref}
      className={cn(
        "inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition disabled:cursor-not-allowed disabled:opacity-50",
        size === "sm" ? "min-h-9 px-3 text-sm" : "min-h-11 px-4 text-sm",
        variant === "primary" &&
          "bg-brand-700 hover:bg-brand-600 text-white shadow-sm",
        variant === "secondary" &&
          "border-ink-950/15 bg-surface text-ink-950 border hover:bg-white",
        variant === "ghost" && "text-ink-800 hover:bg-ink-950/5",
        variant === "danger" &&
          "bg-danger-700 hover:bg-danger-700/90 text-white",
        className,
      )}
      disabled={disabled || busy}
      {...props}
    >
      {busy ? <LoaderCircle className="size-4 animate-spin" /> : null}
      {children}
    </button>
  );
});

const fieldClass =
  "min-h-11 w-full rounded-xl border border-ink-950/15 bg-white px-3 text-sm text-ink-950 shadow-sm placeholder:text-ink-600/65 disabled:bg-ink-950/5";

export const Input = forwardRef<
  HTMLInputElement,
  InputHTMLAttributes<HTMLInputElement>
>(function Input({ className, ...props }, ref) {
  return <input ref={ref} className={cn(fieldClass, className)} {...props} />;
});

export const Select = forwardRef<
  HTMLSelectElement,
  SelectHTMLAttributes<HTMLSelectElement>
>(function Select({ className, children, ...props }, ref) {
  return (
    <select ref={ref} className={cn(fieldClass, className)} {...props}>
      {children}
    </select>
  );
});

export const Textarea = forwardRef<
  HTMLTextAreaElement,
  TextareaHTMLAttributes<HTMLTextAreaElement>
>(function Textarea({ className, ...props }, ref) {
  return (
    <textarea
      ref={ref}
      className={cn(fieldClass, "min-h-28 py-3", className)}
      {...props}
    />
  );
});

export function Field({
  style,
  label,
  hint,
  error,
  required,
  htmlFor,
  children,
}: {
  style?: CSSProperties;
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  htmlFor?: string;
  children: ReactNode;
}) {
  const generatedId = useId();
  const descriptionId = `${generatedId}-description`;
  return (
    <div className="grid gap-1.5" style={style}>
      <label className="text-ink-800 text-sm font-semibold" htmlFor={htmlFor}>
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </label>
      {children}
      {error ? (
        <p id={descriptionId} className="text-danger-700 text-sm" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p id={descriptionId} className="text-ink-600 text-xs">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

export function Card({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={cn(
        "border-ink-950/8 bg-surface shadow-card rounded-2xl border p-4",
        className,
      )}
    >
      {children}
    </section>
  );
}

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "neutral" | "success" | "warning" | "danger" | "info";
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold",
        tone === "neutral" && "bg-ink-950/7 text-ink-800",
        tone === "success" && "bg-brand-100 text-brand-700",
        tone === "warning" && "bg-accent-100 text-accent-600",
        tone === "danger" && "bg-danger-100 text-danger-700",
        tone === "info" && "bg-sky-100 text-sky-800",
      )}
    >
      {children}
    </span>
  );
}

export function Alert({
  title,
  children,
  tone = "info",
}: {
  title?: string;
  children: ReactNode;
  tone?: "info" | "success" | "warning" | "danger";
}) {
  return (
    <div
      className={cn(
        "flex gap-3 rounded-xl border p-4 text-sm",
        tone === "info" && "border-sky-200 bg-sky-50 text-sky-950",
        tone === "success" &&
          "border-brand-600/20 bg-brand-100/60 text-brand-700",
        tone === "warning" &&
          "border-accent-600/20 bg-accent-100/65 text-ink-950",
        tone === "danger" &&
          "border-danger-700/20 bg-danger-100/70 text-danger-700",
      )}
      role={tone === "danger" ? "alert" : "status"}
    >
      {tone === "success" ? (
        <CheckCircle2 className="mt-0.5 size-5 shrink-0" />
      ) : (
        <AlertTriangle className="mt-0.5 size-5 shrink-0" />
      )}
      <div>
        {title ? <p className="font-semibold">{title}</p> : null}
        <div>{children}</div>
      </div>
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        "bg-ink-950/8 h-16 animate-pulse rounded-xl motion-reduce:animate-none",
        className,
      )}
      aria-hidden="true"
    />
  );
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="border-ink-950/15 grid place-items-center gap-3 rounded-2xl border border-dashed p-10 text-center">
      <Inbox className="text-ink-600 size-9" />
      <div>
        <p className="font-semibold">{title}</p>
        {description ? (
          <p className="text-ink-600 mt-1 max-w-md text-sm">{description}</p>
        ) : null}
      </div>
      {action}
    </div>
  );
}

export function Dialog({
  open,
  onClose,
  title,
  children,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (open && !ref.current?.open) ref.current?.showModal();
    if (!open && ref.current?.open) ref.current.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      className="bg-surface text-ink-950 backdrop:bg-ink-950/40 m-auto w-[min(34rem,calc(100%-2rem))] rounded-2xl p-0 shadow-2xl"
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClose={onClose}
    >
      <div className="border-ink-950/10 flex items-center justify-between border-b p-5">
        <h2 className="text-lg font-bold">{title}</h2>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClose}
          aria-label="Close dialog"
        >
          <X className="size-4" />
        </Button>
      </div>
      <div className="p-5">{children}</div>
    </dialog>
  );
}

export function FileUploader({
  label,
  accept,
  onChange,
  disabled,
}: {
  label: string;
  accept?: string;
  onChange: (file: File | null) => void;
  disabled?: boolean;
}) {
  const id = useId();
  return (
    <div className="grid gap-2">
      <label htmlFor={id} className="text-ink-800 text-sm font-semibold">
        {label}
      </label>
      <input
        id={id}
        type="file"
        accept={accept}
        disabled={disabled}
        className="border-ink-950/20 file:bg-brand-100 file:text-brand-700 rounded-xl border border-dashed bg-white p-4 text-sm file:me-3 file:rounded-lg file:border-0 file:px-3 file:py-2 file:font-semibold"
        onChange={(event) => onChange(event.target.files?.[0] ?? null)}
      />
    </div>
  );
}
