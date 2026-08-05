"use client";

import { useMemo } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useSearchParams } from "next/navigation";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Skeleton,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { ApiError, errorMessage } from "@/lib/api/errors";
import { enumCode, statusTone, type EnumKind } from "@/lib/api/enums";
import type { PagedResponse } from "@/lib/api/types";
import { formatDateTime, titleFromCode } from "@/lib/utils";

type Row = Record<string, unknown>;

export type Column = {
  key: string;
  label: string;
  enumKind?: EnumKind;
  dateTime?: boolean;
  render?: (row: Row) => React.ReactNode;
};

function CellValue({ row, column }: { row: Row; column: Column }) {
  const locale = useLocale();
  if (column.render) return column.render(row);
  const value = row[column.key];
  if (column.dateTime && typeof value === "string") {
    return formatDateTime(value, locale);
  }
  if (column.enumKind && (typeof value === "string" || typeof value === "number")) {
    const code = enumCode(column.enumKind, value);
    return <Badge tone={statusTone(code)}>{titleFromCode(code)}</Badge>;
  }
  if (typeof value === "boolean") {
    return (
      <Badge tone={value ? "success" : "neutral"}>{value ? "Yes" : "No"}</Badge>
    );
  }
  if (Array.isArray(value)) return String(value.length);
  return value === null || value === undefined || value === ""
    ? "—"
    : String(value);
}

export function CollectionPage({
  title,
  description,
  endpoint,
  queryKey,
  columns,
  createHref,
  detailHref,
  canCreate = true,
  canDelete = false,
  extraActions,
  realm = "tenant",
}: {
  title: string;
  description?: string;
  endpoint: string;
  queryKey: readonly unknown[];
  columns: Column[];
  createHref?: string;
  detailHref?: (row: Row) => string;
  canCreate?: boolean;
  canDelete?: boolean;
  extraActions?: React.ReactNode;
  realm?: "tenant" | "platform";
}) {
  const t = useTranslations("Common");
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const page = Math.max(1, Number(searchParams.get("page") ?? 1));
  const pageSize = 20;
  const path = `${endpoint}${endpoint.includes("?") ? "&" : "?"}page=${page}&pageSize=${pageSize}`;
  const query = useQuery({
    queryKey: [...queryKey, page],
    queryFn: ({ signal }) =>
      apiRequest<PagedResponse<Row>>(path, { signal, realm }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) =>
      apiRequest(`${endpoint}/${id}`, {
        method: "DELETE",
        realm,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey });
      toast.push("Item cleared successfully.");
    },
  });

  const pageCount = useMemo(
    () =>
      Math.max(1, Math.ceil(Number(query.data?.totalCount ?? 0) / pageSize)),
    [query.data?.totalCount],
  );

  function goToPage(nextPage: number) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("page", String(nextPage));
    router.replace(`${pathname}?${params.toString()}`);
  }

  const headerActions = (
    <>
      {extraActions}
      {createHref && canCreate ? (
        <Button>
          <Link href={createHref} className="inline-flex items-center gap-2">
            <Plus className="size-4" /> {t("create")}
          </Link>
        </Button>
      ) : null}
    </>
  );

  return (
    <>
      <PageHeader
        title={title}
        description={description}
        actions={headerActions}
      />
      {query.isLoading ? (
        <div className="grid gap-3">
          <Skeleton />
          <Skeleton />
          <Skeleton />
        </div>
      ) : query.error ? (
        <Alert
          tone="danger"
          title={
            query.error instanceof ApiError && query.error.status === 403
              ? t("forbidden")
              : "Unable to load data"
          }
        >
          <p>{errorMessage(query.error)}</p>
          <Button
            className="mt-3"
            size="sm"
            variant="secondary"
            onClick={() => void query.refetch()}
          >
            {t("retry")}
          </Button>
        </Alert>
      ) : !query.data?.items.length ? (
        <EmptyState
          title={t("noResults")}
          description="The backend returned an empty page for the current scope."
        />
      ) : (
        <Card className="overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-start text-sm">
              <thead>
                <tr className="border-ink-950/10 bg-ink-950/[0.025] border-b">
                  {columns.map((column) => (
                    <th
                      key={column.key}
                      scope="col"
                      className="text-ink-600 px-4 py-3 text-start text-xs font-bold tracking-wide whitespace-nowrap uppercase"
                    >
                      {column.label}
                    </th>
                  ))}
                  {detailHref || canDelete ? (
                    <th scope="col" className="px-4 py-3 text-end">
                      <span className="sr-only">{t("actions")}</span>
                    </th>
                  ) : null}
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((row, index) => (
                  <tr
                    key={String(row.id ?? row.membershipId ?? index)}
                    className="border-ink-950/7 hover:bg-brand-100/25 border-b last:border-0"
                  >
                    {columns.map((column) => (
                      <td
                        key={column.key}
                        className="max-w-sm px-4 py-3 align-top"
                      >
                        <CellValue row={row} column={column} />
                      </td>
                    ))}
                    {detailHref || canDelete ? (
                      <td className="px-4 py-3 text-end">
                        <div className="inline-flex items-center justify-end gap-3">
                          {detailHref ? (
                            <Link
                              href={detailHref(row)}
                              className="text-brand-700 font-semibold hover:underline"
                            >
                              {t("view")}
                            </Link>
                          ) : null}
                          {canDelete && row.id ? (
                            <button
                              type="button"
                              disabled={deleteMutation.isPending}
                              onClick={() => {
                                if (
                                  confirm(
                                    "Are you sure you want to clear/delete this item?",
                                  )
                                ) {
                                  deleteMutation.mutate(String(row.id));
                                }
                              }}
                              className="text-danger font-semibold hover:underline"
                            >
                              Clear
                            </button>
                          ) : null}
                        </div>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="border-ink-950/8 flex items-center justify-between gap-3 border-t p-3">
            <p className="text-ink-600 text-xs">
              {Number(query.data.totalCount)} total · {t("page", { page })}
            </p>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="secondary"
                disabled={page <= 1}
                onClick={() => goToPage(page - 1)}
              >
                {t("previous")}
              </Button>
              <Button
                size="sm"
                variant="secondary"
                disabled={page >= pageCount}
                onClick={() => goToPage(page + 1)}
              >
                {t("next")}
              </Button>
            </div>
          </div>
        </Card>
      )}
    </>
  );
}
