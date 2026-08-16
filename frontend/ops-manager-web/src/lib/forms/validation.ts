import { z } from "zod";

import { enumCodes } from "@/lib/api/enums";

export const branchSchema = z.object({
  name: z.string().trim().min(2).max(200),
  address: z.string().trim().max(500),
  phone: z.string().trim().max(40),
  timezone: z.string().trim().min(1).max(100),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
});
export type BranchValues = z.infer<typeof branchSchema>;

export const departmentSchema = z.object({
  branchId: z.string().uuid("Select a branch."),
  name: z.string().trim().min(2).max(160),
  description: z.string().trim().max(2000),
  supervisorUserId: z.union([z.literal(""), z.string().uuid()]),
  isActive: z.boolean(),
});
export type DepartmentValues = z.infer<typeof departmentSchema>;

const memberBaseSchema = z.object({
  fullName: z.string().trim().min(2).max(200),
  email: z.union([z.literal(""), z.string().email()]),
  phone: z.string().trim().max(40),
  address: z.string().trim().max(500),
  profileImageUrl: z.string(),
  preferredLanguage: z.enum(["ar", "en", "ru"]),
  role: z.enum(enumCodes.organizationRole),
  temporaryPassword: z.string().max(128),
  departmentIds: z.array(z.string().uuid()),
});

export const memberSchema = memberBaseSchema;
export const createMemberSchema = memberBaseSchema.extend({
  email: z.string().email(),
  temporaryPassword: z
    .string()
    .min(12)
    .max(128)
    .regex(/[A-Z]/, "Include an upper-case letter.")
    .regex(/[a-z]/, "Include a lower-case letter.")
    .regex(/[0-9]/, "Include a number."),
});
export type MemberValues = z.infer<typeof memberSchema>;

export const complaintSchema = z.object({
  branchId: z.string().uuid("Select a branch."),
  targetDepartmentId: z.string(),
  title: z.string().trim().min(2).max(250),
  description: z.string().trim().min(2).max(4000),
  visibility: z.enum(["ManagementOnly", "Participants"]),
});
export type ComplaintValues = z.infer<typeof complaintSchema>;

export const scheduleSchema = z
  .object({
    taskTemplateId: z.string().uuid(),
    branchId: z.string().uuid(),
    departmentId: z.string().uuid(),
    assignmentMode: z.enum([
      "SingleUser",
      "SelectedUsers",
      "AllDepartmentMembers",
    ]),
    assigneeUserIds: z.array(z.string().uuid()),
    recurrenceType: z.enum(["Daily", "Weekly", "Monthly", "SpecificDates"]),
    weekdays: z.array(z.number().int().min(0).max(6)),
    monthDays: z.array(z.number().int().min(1).max(31)),
    includeLastDayOfMonth: z.boolean(),
    specificDates: z.array(z.string().min(1)),
    recurrenceStartDate: z.string().min(1),
    recurrenceEndDate: z.string(),
    executionStartTime: z.string().min(1),
    executionDueTime: z.string().min(1),
    executionDueDayOffset: z.number().int().min(0).max(1),
    isActive: z.boolean(),
  })
  .superRefine((values, context) => {
    const assignmentIsValid =
      values.assignmentMode === "SingleUser"
        ? values.assigneeUserIds.length === 1
        : values.assignmentMode === "SelectedUsers"
          ? values.assigneeUserIds.length >= 2
          : values.assigneeUserIds.length === 0;
    if (!assignmentIsValid) {
      context.addIssue({
        code: "custom",
        path: ["assigneeUserIds"],
        message: "Choose the employees required by the assignment mode.",
      });
    }
    if (
      values.recurrenceType === "Weekly" &&
      values.weekdays.length === 0
    ) {
      context.addIssue({
        code: "custom",
        path: ["weekdays"],
        message: "Select at least one weekday.",
      });
    }
    if (
      values.recurrenceType === "Monthly" &&
      values.monthDays.length === 0 &&
      !values.includeLastDayOfMonth
    ) {
      context.addIssue({
        code: "custom",
        path: ["monthDays"],
        message:
          "Select at least one day of the month or enable last day of month.",
      });
    }
    if (
      values.recurrenceType === "SpecificDates" &&
      values.specificDates.length === 0
    ) {
      context.addIssue({
        code: "custom",
        path: ["specificDates"],
        message: "Select at least one specific date.",
      });
    }
    if (
      values.executionDueDayOffset === 0 &&
      values.executionStartTime &&
      values.executionDueTime <= values.executionStartTime
    ) {
      context.addIssue({
        code: "custom",
        path: ["executionDueTime"],
        message: "Same-day due time must be after the start time.",
      });
    }
  });
export type ScheduleValues = z.infer<typeof scheduleSchema>;

export const planSchema = z.object({
  name: z.string().trim().min(2).max(200),
  code: z.string().trim().min(2).max(50),
  description: z.string().trim().max(2000),
  monthlyPrice: z.string(),
  yearlyPrice: z.string(),
  currency: z.string().trim().length(3),
  maxUsers: z.number().int().positive(),
  maxBranches: z.number().int().positive(),
  maxStorageMb: z.number().int().min(0),
  features: z.string(),
  gracePeriodDays: z.number().int().min(0),
  isActive: z.boolean(),
});
export type PlanValues = z.infer<typeof planSchema>;

export const paymentSchema = z
  .object({
    organizationId: z.string().uuid(),
    amount: z.number().min(0),
    currency: z.string().trim().length(3),
    paymentMethod: z.enum(enumCodes.paymentMethod),
    paymentReference: z.string().trim().max(200),
    paidAt: z.string(),
    periodStart: z.string().min(1),
    periodEnd: z.string().min(1),
    receiptFileUrl: z.string().trim(),
    note: z.string().trim().max(2000),
    activateSubscription: z.boolean(),
    activationPlanId: z.string(),
    activationEndsAt: z.string(),
  })
  .refine((value) => value.periodEnd >= value.periodStart, {
    path: ["periodEnd"],
    message: "The payment period end must not precede its start.",
  })
  .refine(
    (value) =>
      !value.activateSubscription ||
      (Boolean(value.activationPlanId) && Boolean(value.activationEndsAt)),
    {
      path: ["activationPlanId"],
      message: "Plan and end date are required when activating.",
    },
  );
export type PaymentValues = z.infer<typeof paymentSchema>;
