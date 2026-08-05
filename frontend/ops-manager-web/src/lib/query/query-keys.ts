export const queryKeys = {
  auth: {
    tenant: ["auth", "tenant"] as const,
    platform: ["auth", "platform"] as const,
  },
  organization: {
    root: ["organization"] as const,
    branchesRoot: ["organization", "branches"] as const,
    branches: (filters: object = {}) =>
      ["organization", "branches", filters] as const,
    branch: (id: string) => ["organization", "branches", id] as const,
    departmentsRoot: ["organization", "departments"] as const,
    departments: (filters: object = {}) =>
      ["organization", "departments", filters] as const,
    department: (id: string) => ["organization", "departments", id] as const,
    membersRoot: ["organization", "members"] as const,
    members: (filters: object = {}) =>
      ["organization", "members", filters] as const,
    member: (id: string) => ["organization", "members", id] as const,
  },
  taskTemplates: {
    lists: (filters: object = {}) => ["task-templates", filters] as const,
    detail: (id: string) => ["task-templates", id] as const,
  },
  taskSchedules: {
    lists: (filters: object = {}) => ["task-schedules", filters] as const,
    detail: (id: string) => ["task-schedules", id] as const,
  },
  tasks: {
    lists: (filters: object = {}) => ["tasks", filters] as const,
    detail: (id: string) => ["tasks", id] as const,
    calendar: (filters: object = {}) => ["tasks", "calendar", filters] as const,
  },
  orderTemplates: {
    lists: (filters: object = {}) => ["order-templates", filters] as const,
    detail: (id: string) => ["order-templates", id] as const,
  },
  orders: {
    lists: (scope: string, filters: object = {}) =>
      ["department-orders", scope, filters] as const,
    detail: (id: string) => ["department-orders", id] as const,
  },
  complaints: {
    lists: (filters: object = {}) => ["complaints", filters] as const,
    detail: (id: string) => ["complaints", id] as const,
  },
  reports: (name: string, filters: object = {}) =>
    ["reports", name, filters] as const,
  notifications: {
    lists: (filters: object = {}) => ["notifications", filters] as const,
    unread: ["notifications", "unread"] as const,
  },
  platform: {
    organizations: (filters: object = {}) =>
      ["platform", "organizations", filters] as const,
    organization: (id: string) => ["platform", "organizations", id] as const,
    organizationBranches: (organizationId: string) =>
      ["platform", "organizations", organizationId, "branches"] as const,
    organizationBranch: (organizationId: string, branchId: string) =>
      [
        "platform",
        "organizations",
        organizationId,
        "branches",
        branchId,
      ] as const,
    plans: (filters: object = {}) => ["platform", "plans", filters] as const,
    plan: (id: string) => ["platform", "plans", id] as const,
    payments: (filters: object = {}) =>
      ["platform", "payments", filters] as const,
    payment: (id: string) => ["platform", "payments", id] as const,
    reports: (name: string) => ["platform", "reports", name] as const,
  },
};
