import type { Schemas } from "@/lib/api/types";
import { relatedNotificationHref } from "@/lib/notifications/related-href";

function notification(
  relatedEntityType: string | null,
  relatedEntityId: string | null,
): Schemas["NotificationDto"] {
  return {
    id: "11111111-1111-4111-8111-111111111111",
    type: 0,
    title: "Update",
    body: "Body",
    parameters: {},
    relatedEntityType,
    relatedEntityId,
    isRead: false,
    readAt: null,
    createdAt: "2030-01-01T00:00:00Z",
  };
}

describe("notification destinations", () => {
  it("links known workflow entities", () => {
    expect(
      relatedNotificationHref(
        notification("Task", "22222222-2222-4222-8222-222222222222"),
      ),
    ).toBe("/tasks/22222222-2222-4222-8222-222222222222");
  });

  it("does not invent links for unsupported entity types", () => {
    expect(
      relatedNotificationHref(
        notification("Unknown", "22222222-2222-4222-8222-222222222222"),
      ),
    ).toBeNull();
  });
});
