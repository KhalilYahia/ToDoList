import {
  branchSchema,
  complaintSchema,
  createMemberSchema,
  departmentSchema,
  paymentSchema,
  planSchema,
  scheduleSchema,
} from "@/lib/forms/validation";

describe("MVP form validation", () => {
  it("validates platform branch fields", () => {
    expect(
      branchSchema.safeParse({
        name: "Central",
        address: "",
        phone: "",
        timezone: "Europe/Moscow",
        isPrimary: true,
        isActive: true,
      }).success,
    ).toBe(true);

    expect(
      branchSchema.safeParse({
        name: "A",
        address: "",
        phone: "",
        timezone: "",
        isPrimary: false,
        isActive: true,
      }).success,
    ).toBe(false);
  });

  it("requires a branch for a department", () => {
    expect(
      departmentSchema.safeParse({
        branchId: "",
        name: "Kitchen",
        description: "",
        supervisorUserId: "",
        isActive: true,
      }).success,
    ).toBe(false);
  });

  it("accepts only supported member roles and strong temporary passwords", () => {
    const values = {
      fullName: "Alex Manager",
      email: "alex@example.test",
      phone: "",
      preferredLanguage: "en",
      role: "Supervisor",
      temporaryPassword: "Temporary1234",
      departmentIds: ["11111111-1111-4111-8111-111111111111"],
    };
    expect(createMemberSchema.safeParse(values).success).toBe(true);
    expect(
      createMemberSchema.safeParse({
        ...values,
        role: "DepartmentAdmin",
      }).success,
    ).toBe(false);
  });

  it("requires a usable complaint title and branch", () => {
    const result = complaintSchema.safeParse({
      branchId: "",
      targetDepartmentId: "",
      title: "",
      description: "Details",
      visibility: "Participants",
    });
    expect(result.success).toBe(false);
  });

  it("requires weekdays for weekly schedules", () => {
    const result = scheduleSchema.safeParse({
      taskTemplateId: "11111111-1111-4111-8111-111111111111",
      branchId: "22222222-2222-4222-8222-222222222222",
      departmentId: "33333333-3333-4333-8333-333333333333",
      assignmentMode: "SingleUser",
      assigneeUserIds: ["44444444-4444-4444-8444-444444444444"],
      recurrenceType: "Weekly",
      weekdays: [],
      monthDays: [],
      includeLastDayOfMonth: false,
      specificDates: [],
      recurrenceStartDate: "2030-01-01",
      recurrenceEndDate: "",
      executionStartTime: "09:00",
      executionDueTime: "10:00",
      executionDueDayOffset: 0,
      isActive: true,
    });
    expect(result.success).toBe(false);
  });

  it("requires at least two users for selected-user schedules", () => {
    const result = scheduleSchema.safeParse({
      taskTemplateId: "11111111-1111-4111-8111-111111111111",
      branchId: "22222222-2222-4222-8222-222222222222",
      departmentId: "33333333-3333-4333-8333-333333333333",
      assignmentMode: "SelectedUsers",
      assigneeUserIds: ["44444444-4444-4444-8444-444444444444"],
      recurrenceType: "Daily",
      weekdays: [],
      monthDays: [],
      includeLastDayOfMonth: false,
      specificDates: [],
      recurrenceStartDate: "2030-01-01",
      recurrenceEndDate: "",
      executionStartTime: "09:00",
      executionDueTime: "10:00",
      executionDueDayOffset: 0,
      isActive: true,
    });
    expect(result.success).toBe(false);
  });

  it("validates plan limits and a three-letter currency", () => {
    const result = planSchema.safeParse({
      name: "Starter",
      code: "starter",
      description: "",
      monthlyPrice: "10",
      yearlyPrice: "100",
      currency: "USD",
      maxUsers: 10,
      maxBranches: 1,
      maxStorageMb: 100,
      features: "tasks=true",
      gracePeriodDays: 7,
      isActive: true,
    });
    expect(result.success).toBe(true);
  });

  it("requires activation details when payment activation is selected", () => {
    const result = paymentSchema.safeParse({
      organizationId: "11111111-1111-4111-8111-111111111111",
      amount: 100,
      currency: "USD",
      paymentMethod: "BankTransfer",
      paymentReference: "",
      paidAt: "",
      periodStart: "2030-01-01",
      periodEnd: "2030-02-01",
      receiptFileUrl: "",
      note: "",
      activateSubscription: true,
      activationPlanId: "",
      activationEndsAt: "",
    });
    expect(result.success).toBe(false);
  });
});
