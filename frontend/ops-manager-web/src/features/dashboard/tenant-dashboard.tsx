"use client";

import { useMemo } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useQueries } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowUpRight,
  ClipboardCheck,
  PackageCheck,
} from "lucide-react";

import { PageHeader } from "@/components/layout/page-header";
import { Badge, Card, EmptyState, Skeleton } from "@/components/ui/primitives";
import { Link } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import { isManager } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";
import { formatDateTime } from "@/lib/utils";

type Task = Schemas["TaskDto"];
type Order = Schemas["DepartmentOrderDto"];

function StatCard({
  label,
  value,
  href,
  warning,
}: {
  label: string;
  value: number;
  href: string;
  warning?: boolean;
}) {
  return (
    <Link href={href}>
      <Card className="group hover:border-brand-600/25 h-full transition hover:-translate-y-0.5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-ink-600 text-sm font-semibold">{label}</p>
            <p className="mt-2 text-3xl font-black tracking-tight">{value}</p>
          </div>
          <span
            className={`grid size-10 place-items-center rounded-xl ${
              warning
                ? "bg-accent-100 text-accent-600"
                : "bg-brand-100 text-brand-700"
            }`}
          >
            {warning ? (
              <AlertTriangle className="size-5" />
            ) : (
              <ArrowUpRight className="size-5 rtl:-rotate-90" />
            )}
          </span>
        </div>
      </Card>
    </Link>
  );
}

export function TenantDashboard() {
  const t = useTranslations("Dashboard");
  const locale = useLocale();
  const { identity } = useAuth();
  const manager = isManager(identity);
  const range = useMemo(() => {
    const from = new Date();
    from.setHours(0, 0, 0, 0);
    const to = new Date(from);
    to.setDate(to.getDate() + 1);
    return { from: from.toISOString(), to: to.toISOString() };
  }, []);
  const rangeQuery = `from=${encodeURIComponent(range.from)}&to=${encodeURIComponent(range.to)}`;

  const [tasksSummary, ordersSummary, complaintsSummary, myTasks, incoming] =
    useQueries({
      queries: [
        {
          queryKey: queryKeys.reports("dashboard-tasks", range),
          queryFn: () =>
            apiRequest<Schemas["TaskSummaryReportDto"]>(
              `/reports/tasks/summary?${rangeQuery}`,
            ),
          enabled: manager,
        },
        {
          queryKey: queryKeys.reports("dashboard-orders", range),
          queryFn: () =>
            apiRequest<Schemas["OrderSummaryReportDto"]>(
              `/reports/department-orders/summary?${rangeQuery}`,
            ),
          enabled: manager,
        },
        {
          queryKey: queryKeys.reports("dashboard-complaints", range),
          queryFn: () =>
            apiRequest<Schemas["ComplaintSummaryReportDto"]>(
              `/reports/complaints/summary?${rangeQuery}`,
            ),
          enabled: manager,
        },
        {
          queryKey: queryKeys.tasks.lists({ mine: true, pageSize: 5 }),
          queryFn: () =>
            apiRequest<PagedResponse<Task>>("/tasks/my?page=1&pageSize=5"),
        },
        {
          queryKey: queryKeys.orders.lists("incoming", { pageSize: 5 }),
          queryFn: () =>
            apiRequest<PagedResponse<Order>>(
              "/department-orders/incoming?page=1&pageSize=5",
            ),
        },
      ],
    });

  return (
    <>
      <PageHeader title={t("title")} description={t("subtitle")} />
      {manager ? (
        <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          {tasksSummary.isLoading ? (
            Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-32" />
            ))
          ) : (
            <>
              <StatCard
                label={t("todayTasks")}
                value={Number(tasksSummary.data?.total ?? 0)}
                href="/tasks"
              />
              <StatCard
                label={t("overdue")}
                value={Number(tasksSummary.data?.overdue ?? 0)}
                href="/tasks?overdue=true"
                warning
              />
              <StatCard
                label={t("inProgress")}
                value={Number(tasksSummary.data?.inProgress ?? 0)}
                href="/tasks"
              />
              <StatCard
                label={t("incomingOrders")}
                value={Number(ordersSummary.data?.total ?? 0)}
                href="/department-orders/incoming"
              />
              <StatCard
                label={t("openComplaints")}
                value={Number(complaintsSummary.data?.open ?? 0)}
                href="/complaints"
                warning
              />
            </>
          )}
        </div>
      ) : null}

      <div className="grid gap-5 xl:grid-cols-2">
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-lg font-black">
              <ClipboardCheck className="text-brand-700 size-5" />
              {t("nextTasks")}
            </h2>
            <Link href="/my-tasks" className="text-brand-700 text-sm font-bold">
              View all
            </Link>
          </div>
          {myTasks.isLoading ? (
            <Skeleton className="h-40" />
          ) : !myTasks.data?.items.length ? (
            <EmptyState title="No assigned tasks" />
          ) : (
            <ul className="divide-ink-950/8 divide-y">
              {myTasks.data.items.map((task) => (
                <li key={task.id} className="flex items-center gap-3 py-3">
                  <div className="min-w-0 flex-1">
                    <Link
                      href={`/tasks/${task.id}`}
                      className="hover:text-brand-700 font-bold"
                    >
                      {task.title}
                    </Link>
                    <p className="text-ink-600 text-xs">
                      {formatDateTime(task.dueAt, locale)}
                    </p>
                  </div>
                  {task.isOverdue ? <Badge tone="danger">Overdue</Badge> : null}
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-lg font-black">
              <PackageCheck className="text-brand-700 size-5" />
              {t("incomingOrders")}
            </h2>
            <Link
              href="/department-orders/incoming"
              className="text-brand-700 text-sm font-bold"
            >
              View all
            </Link>
          </div>
          {incoming.isLoading ? (
            <Skeleton className="h-40" />
          ) : !incoming.data?.items.length ? (
            <EmptyState title="No incoming orders" />
          ) : (
            <ul className="divide-ink-950/8 divide-y">
              {incoming.data.items.map((order) => (
                <li key={order.id} className="flex items-center gap-3 py-3">
                  <div className="min-w-0 flex-1">
                    <Link
                      href={`/department-orders/${order.id}`}
                      className="hover:text-brand-700 font-bold"
                    >
                      {order.orderNumber}
                    </Link>
                    <p className="text-ink-600 text-xs">
                      {formatDateTime(order.requiredAt, locale)}
                    </p>
                  </div>
                  {order.isLate ? <Badge tone="danger">Late</Badge> : null}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </>
  );
}
