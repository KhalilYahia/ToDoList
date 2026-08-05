"use client";

import { useQuery } from "@tanstack/react-query";

import { PageHeader } from "@/components/layout/page-header";
import { Alert, Card, Skeleton } from "@/components/ui/primitives";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

export function PlatformDashboard() {
  const subscriptions = useQuery({
    queryKey: queryKeys.platform.reports("subscriptions"),
    queryFn: () =>
      apiRequest<Schemas["SubscriptionSummaryReportDto"]>(
        "/platform/reports/subscriptions/summary",
        { realm: "platform" },
      ),
  });
  const payments = useQuery({
    queryKey: queryKeys.platform.reports("payments"),
    queryFn: () =>
      apiRequest<Schemas["PaymentSummaryReportDto"]>(
        "/platform/reports/payments/summary",
        { realm: "platform" },
      ),
  });

  return (
    <>
      <PageHeader
        title="Platform overview"
        description="Subscription and confirmed-payment summaries across organizations."
      />
      {subscriptions.isLoading ? (
        <div className="grid gap-4 sm:grid-cols-3">
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
        </div>
      ) : subscriptions.error ? (
        <Alert tone="danger">{errorMessage(subscriptions.error)}</Alert>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {Object.entries(subscriptions.data ?? {}).map(([key, value]) => (
            <Card key={key}>
              <p className="text-ink-600 text-sm font-semibold">{key}</p>
              <p className="mt-2 text-3xl font-black">{String(value)}</p>
            </Card>
          ))}
        </div>
      )}
      <Card className="mt-5">
        <h2 className="mb-4 text-lg font-black">Confirmed payments</h2>
        <div className="grid gap-3 sm:grid-cols-3">
          {payments.data?.byCurrency.map((item) => (
            <div
              key={item.currency}
              className="bg-ink-950/[0.035] rounded-xl p-4"
            >
              <p className="text-sm font-bold">{item.currency}</p>
              <p className="mt-1 text-xl font-black">
                {String(item.confirmedAmount)}
              </p>
              <p className="text-ink-600 text-xs">
                {String(item.confirmedCount)} payments
              </p>
            </div>
          ))}
        </div>
      </Card>
    </>
  );
}
