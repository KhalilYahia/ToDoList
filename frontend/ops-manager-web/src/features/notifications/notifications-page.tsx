"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale } from "next-intl";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Skeleton,
} from "@/components/ui/primitives";
import { Link } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { relatedNotificationHref } from "@/lib/notifications/related-href";
import { queryKeys } from "@/lib/query/query-keys";
import { formatDateTime } from "@/lib/utils";

export function NotificationsPage() {
  const locale = useLocale();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: queryKeys.notifications.lists({ page: 1 }),
    queryFn: () =>
      apiRequest<PagedResponse<Schemas["NotificationDto"]>>(
        "/notifications?page=1&pageSize=50",
      ),
  });
  const markMutation = useMutation({
    mutationFn: (id: string | "all") =>
      apiRequest(
        id === "all" ? "/notifications/read-all" : `/notifications/${id}/read`,
        { method: "POST" },
      ),
    onSuccess: () => {
      void query.refetch();
      void queryClient.invalidateQueries({
        queryKey: queryKeys.notifications.unread,
      });
    },
  });

  return (
    <>
      <PageHeader
        title="Notifications"
        description="System text follows your locale; user-created content is preserved."
        actions={
          <Button
            variant="secondary"
            disabled={
              markMutation.isPending ||
              !query.data?.items.some((notification) => !notification.isRead)
            }
            onClick={() => markMutation.mutate("all")}
          >
            Mark all read
          </Button>
        }
      />
      {markMutation.error ? (
        <Alert tone="danger">{errorMessage(markMutation.error)}</Alert>
      ) : null}
      {query.isLoading ? (
        <div className="grid gap-3">
          <Skeleton />
          <Skeleton />
          <Skeleton />
        </div>
      ) : query.error ? (
        <Alert tone="danger">{errorMessage(query.error)}</Alert>
      ) : !query.data?.items.length ? (
        <EmptyState
          title="No notifications"
          description="New workflow updates will appear here."
        />
      ) : (
        <Card className="divide-ink-950/8 divide-y p-0">
          {query.data.items.map((notification) => {
            const href = relatedNotificationHref(notification);
            return (
              <article
                key={notification.id}
                className="grid gap-3 p-4 md:grid-cols-[1fr_auto] md:items-center"
              >
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-bold">{notification.title}</h2>
                    {!notification.isRead ? (
                      <Badge tone="info">Unread</Badge>
                    ) : null}
                  </div>
                  <p className="text-ink-800 mt-1 text-sm">
                    {notification.body}
                  </p>
                  <time className="text-ink-600 mt-1 block text-xs">
                    {formatDateTime(notification.createdAt, locale)}
                  </time>
                </div>
                <div className="flex flex-wrap gap-2">
                  {href ? (
                    <Button variant="secondary" size="sm">
                      <Link href={href}>Open</Link>
                    </Button>
                  ) : null}
                  {!notification.isRead ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={markMutation.isPending}
                      onClick={() => markMutation.mutate(notification.id)}
                    >
                      Mark read
                    </Button>
                  ) : null}
                </div>
              </article>
            );
          })}
        </Card>
      )}
    </>
  );
}
