"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Card,
  EmptyState,
  Field,
  Input,
  Skeleton,
} from "@/components/ui/primitives";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { PagedResponse } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

type ReportKind = "tasks" | "department-orders" | "complaints";

function defaultDates() {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 30);
  return {
    from: from.toISOString().slice(0, 10),
    to: to.toISOString().slice(0, 10),
  };
}

function Metric({ label, value }: { label: string; value: unknown }) {
  return (
    <Card>
      <p className="text-ink-600 text-sm font-semibold">{label}</p>
      <p className="mt-2 text-2xl font-black">
        {value === null || value === undefined ? "—" : String(value)}
      </p>
    </Card>
  );
}

export function ReportsPage({ kind }: { kind: ReportKind }) {
  const [initial] = useState(() => defaultDates());
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const queryString = `from=${encodeURIComponent(new Date(`${from}T00:00:00`).toISOString())}&to=${encodeURIComponent(new Date(`${to}T23:59:59.999`).toISOString())}`;
  const summaryPath =
    kind === "tasks"
      ? "/reports/tasks/summary"
      : kind === "department-orders"
        ? "/reports/department-orders/summary"
        : "/reports/complaints/summary";

  const summary = useQuery({
    queryKey: queryKeys.reports(`${kind}-summary`, { from, to }),
    queryFn: ({ signal }) =>
      apiRequest<Record<string, unknown>>(`${summaryPath}?${queryString}`, {
        signal,
      }),
  });
  const breakdown = useQuery({
    queryKey: queryKeys.reports(`${kind}-breakdown`, { from, to }),
    queryFn: ({ signal }) => {
      const path =
        kind === "tasks"
          ? `/reports/tasks/by-department?${queryString}&page=1&pageSize=20`
          : kind === "department-orders"
            ? `/reports/department-orders/by-route?${queryString}&page=1&pageSize=20`
            : "";
      return path
        ? apiRequest<PagedResponse<Record<string, unknown>>>(path, { signal })
        : Promise.resolve({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    },
  });

  const title =
    kind === "tasks"
      ? "Task reports"
      : kind === "department-orders"
        ? "Department-order reports"
        : "Complaint reports";

  return (
    <>
      <PageHeader
        title={title}
        description="Metrics are returned by backend report definitions; the client does not recalculate partial list data."
      />
      <Card className="mb-5">
        <div className="grid gap-4 sm:grid-cols-2 lg:max-w-2xl">
          <Field label="From">
            <Input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </Field>
          <Field label="To">
            <Input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </Field>
        </div>
      </Card>
      {summary.isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
        </div>
      ) : summary.error ? (
        <Alert tone="danger">{errorMessage(summary.error)}</Alert>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {Object.entries(summary.data ?? {}).map(([key, value]) => (
            <Metric
              key={key}
              label={key.replace(/([a-z])([A-Z])/g, "$1 $2")}
              value={value}
            />
          ))}
        </div>
      )}
      <Card className="mt-5">
        <h2 className="mb-4 text-lg font-black">Breakdown</h2>
        {!breakdown.data?.items.length ? (
          <EmptyState
            title={
              kind === "complaints"
                ? "No breakdown endpoint"
                : "No breakdown rows for this period"
            }
            description={
              kind === "complaints"
                ? "The backend currently exposes complaint summary metrics only."
                : undefined
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-ink-950/10 border-b">
                  {Object.keys(breakdown.data.items[0] ?? {}).map((key) => (
                    <th key={key} className="px-3 py-2 text-start">
                      {key}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {breakdown.data.items.map((row, index) => (
                  <tr key={index} className="border-ink-950/7 border-b">
                    {Object.values(row).map((value, cell) => (
                      <td key={cell} className="px-3 py-2">
                        {String(value ?? "—")}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </>
  );
}
