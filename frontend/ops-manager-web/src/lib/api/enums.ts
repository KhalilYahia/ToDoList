export const enumCodes = {
  organizationRole: ["Manager", "Supervisor", "Employee"],
  platformRole: ["Administrator", "Support"],
  organizationStatus: ["Active", "Suspended", "Archived"],
  accountStatus: ["Active", "Suspended", "Disabled"],
  subscriptionAccess: ["Full", "GraceLimited", "ReadOnly", "Blocked"],
  priority: ["Low", "Normal", "High", "Urgent"],
  taskStatus: [
    "NotStarted",
    "InProgress",
    "Blocked",
    "PendingApproval",
    "Returned",
    "Completed",
    "Cancelled",
  ],
  taskItemStatus: ["Pending", "Completed", "Skipped"],
  taskItemType: [
    "Question",
    "RatingSlider",
    "SingleLineText",
    "MultiLineText",
    "MultipleChoice",
    "Instruction",
  ],
  evidenceMode: ["None", "Optional", "Required"],
  taskAssignmentMode: ["SingleUser", "SelectedUsers", "AllDepartmentMembers"],
  taskExecutionWindowState: ["NotOpen", "Open", "Expired"],
  taskTemporalScope: ["Upcoming", "Past"],
  recurrence: ["Daily", "Weekly", "Monthly", "SpecificDates"],
  orderStatus: [
    "Draft",
    "Submitted",
    "Accepted",
    "Preparing",
    "Ready",
    "Delivered",
    "Received",
    "Rejected",
    "Cancelled",
  ],
  orderItemStatus: [
    "Pending",
    "Preparing",
    "Ready",
    "Fulfilled",
    "PartiallyFulfilled",
    "Rejected",
  ],
  unit: [
    "Each",
    "Kilogram",
    "Gram",
    "Liter",
    "Milliliter",
    "Meter",
    "Centimeter",
    "Box",
    "Package",
    "Custom",
  ],
  complaintStatus: [
    "Submitted",
    "UnderReview",
    "InProgress",
    "Resolved",
    "Closed",
    "Rejected",
  ],
  complaintVisibility: ["ManagementOnly", "Participants"],
  subscriptionStatus: [
    "Trial",
    "Active",
    "GracePeriod",
    "Expired",
    "Suspended",
    "Cancelled",
    "Complimentary",
  ],
  billingMode: ["Trial", "MonthlyBilling", "Yearly", "Manual"],
  paymentMethod: ["Cash", "BankTransfer", "CardTerminal", "Other"],
  paymentStatus: ["Pending", "Confirmed", "Rejected", "Refunded"],
} as const;

export type EnumKind = keyof typeof enumCodes;

export function enumCode(
  kind: EnumKind,
  value: number | string | null | undefined,
): string {
  if (typeof value === "number") return enumCodes[kind][value] ?? String(value);
  return value ?? "—";
}

export function enumValue(kind: EnumKind, code: string): number {
  return enumCodes[kind].indexOf(code as never);
}

export function statusTone(
  code: string,
): "neutral" | "success" | "warning" | "danger" | "info" {
  if (
    [
      "Active",
      "Completed",
      "Received",
      "Resolved",
      "Confirmed",
      "Full",
    ].includes(code)
  ) {
    return "success";
  }
  if (
    [
      "Rejected",
      "Cancelled",
      "Expired",
      "Suspended",
      "Blocked",
      "Disabled",
    ].includes(code)
  ) {
    return "danger";
  }
  if (
    [
      "Urgent",
      "PendingApproval",
      "Returned",
      "GracePeriod",
      "GraceLimited",
      "PartiallyFulfilled",
    ].includes(code)
  ) {
    return "warning";
  }
  if (["InProgress", "Preparing", "Ready", "Delivered"].includes(code)) {
    return "info";
  }
  return "neutral";
}
