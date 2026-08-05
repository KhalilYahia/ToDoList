import type { Schemas } from "@/lib/api/types";

export function relatedNotificationHref(
  notification: Schemas["NotificationDto"],
): string | null {
  if (!notification.relatedEntityId) return null;
  const routes: Record<string, string> = {
    Task: "/tasks",
    DepartmentOrder: "/department-orders",
    Complaint: "/complaints",
  };
  const base = notification.relatedEntityType
    ? routes[notification.relatedEntityType]
    : undefined;
  return base ? `${base}/${notification.relatedEntityId}` : null;
}
