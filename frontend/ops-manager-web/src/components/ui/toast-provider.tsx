"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { X } from "lucide-react";

type Toast = { id: number; message: string; tone: "success" | "danger" };
type ToastContextValue = {
  push: (message: string, tone?: Toast["tone"]) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const push = useCallback(
    (message: string, tone: Toast["tone"] = "success") => {
      const id = Date.now();
      setToasts((current) => [...current, { id, message, tone }]);
      window.setTimeout(
        () =>
          setToasts((current) => current.filter((toast) => toast.id !== id)),
        5000,
      );
    },
    [],
  );

  const value = useMemo(() => ({ push }), [push]);
  return (
    <ToastContext.Provider value={value}>
      {children}
      <div
        className="fixed end-4 bottom-4 z-50 grid w-[min(24rem,calc(100%-2rem))] gap-2"
        aria-live="polite"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`flex items-center justify-between gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-white shadow-xl ${
              toast.tone === "success" ? "bg-brand-700" : "bg-danger-700"
            }`}
          >
            {toast.message}
            <button
              aria-label="Dismiss notification"
              onClick={() =>
                setToasts((current) =>
                  current.filter((item) => item.id !== toast.id),
                )
              }
            >
              <X className="size-4" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used within ToastProvider.");
  return context;
}
