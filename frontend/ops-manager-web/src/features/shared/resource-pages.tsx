"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";

import { fetchAllPages } from "@/lib/api/pagination";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  isManager,
  isPlatformAdministrator,
  isSupervisorOrManager,
} from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";

import { CollectionPage, type Column } from "./collection-page";

const branchColumns: Column[] = [
  { key: "name", label: "Name" },
  { key: "timezone", label: "Timezone" },
  { key: "isPrimary", label: "Primary" },
  { key: "isActive", label: "Active" },
];
const memberColumns: Column[] = [
  { key: "fullName", label: "Name" },
  { key: "email", label: "Email" },
  { key: "role", label: "Role", enumKind: "organizationRole" },
  { key: "accountStatus", label: "Account", enumKind: "accountStatus" },
  { key: "isActive", label: "Membership" },
];
const taskTemplateColumns: Column[] = [
  { key: "title", label: "Title" },
  { key: "defaultPriority", label: "Priority", enumKind: "priority" },
  { key: "defaultDurationMinutes", label: "Minutes" },
  { key: "requiresApproval", label: "Approval" },
  { key: "isActive", label: "Active" },
];
const taskScheduleColumns: Column[] = [
  { key: "taskTemplateId", label: "Template" },
  { key: "recurrenceType", label: "Recurrence", enumKind: "recurrence" },
  { key: "recurrenceStartDate", label: "Start date" },
  { key: "recurrenceEndDate", label: "End date" },
  { key: "isActive", label: "Active" },
];
const taskColumns: Column[] = [
  { key: "title", label: "Title" },
  { key: "status", label: "Status", enumKind: "taskStatus" },
  { key: "priority", label: "Priority", enumKind: "priority" },
  { key: "dueAt", label: "Due", dateTime: true },
  { key: "isOverdue", label: "Overdue" },
];
const orderTemplateColumns: Column[] = [
  { key: "name", label: "Name" },
  { key: "sourceDepartmentId", label: "Source" },
  { key: "targetDepartmentId", label: "Target" },
  { key: "allowCustomItems", label: "Custom items" },
  { key: "isActive", label: "Active" },
];
const orderColumns: Column[] = [
  { key: "orderNumber", label: "Order" },
  { key: "status", label: "Status", enumKind: "orderStatus" },
  { key: "priority", label: "Priority", enumKind: "priority" },
  { key: "requiredAt", label: "Required", dateTime: true },
  { key: "isLate", label: "Late" },
];
const complaintColumns: Column[] = [
  { key: "complaintNumber", label: "Complaint" },
  { key: "title", label: "Title" },
  { key: "status", label: "Status", enumKind: "complaintStatus" },
  { key: "visibility", label: "Visibility", enumKind: "complaintVisibility" },
];

export function BranchesPage() {
  const t = useTranslations("Pages");
  return (
    <CollectionPage
      title={t("branches")}
      description="View operating locations. Only a platform administrator can add, update, or delete branches."
      endpoint="/branches"
      queryKey={queryKeys.organization.branches()}
      columns={branchColumns}
      detailHref={(row) => `/settings/branches/${String(row.id)}`}
    />
  );
}

export function DepartmentsPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  const manager = isManager(identity);
  const branches = useQuery({
    queryKey: queryKeys.organization.branches({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["BranchDto"]>("/branches", { signal }),
  });
  const members = useQuery({
    queryKey: queryKeys.organization.members({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["MemberDto"]>("/members", { signal }),
    enabled: manager,
  });
  const branchNames = new Map(
    (branches.data ?? []).map((branch) => [branch.id, branch.name]),
  );
  const memberNames = new Map(
    (members.data ?? []).map((member) => [member.userId, member.fullName]),
  );
  return (
    <CollectionPage
      title={t("departments")}
      description="Create departments under a branch, assign members, and manage supervisors."
      endpoint="/departments"
      queryKey={queryKeys.organization.departments()}
      columns={[
        { key: "name", label: "Name" },
        {
          key: "branchId",
          label: "Branch",
          render: (row) =>
            branchNames.get(String(row.branchId)) ?? String(row.branchId),
        },
        {
          key: "supervisorUserId",
          label: "Supervisor",
          render: (row) => {
            const supervisorId = String(row.supervisorUserId ?? "");
            return supervisorId
              ? (memberNames.get(supervisorId) ?? supervisorId)
              : "—";
          },
        },
        { key: "isActive", label: "Active" },
      ]}
      createHref="/settings/departments/new"
      detailHref={(row) => `/settings/departments/${String(row.id)}`}
      canCreate={manager}
    />
  );
}

export function MembersPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("members")}
      description="Create members with a temporary password, assign roles, and scope department access."
      endpoint="/members"
      queryKey={queryKeys.organization.members()}
      columns={memberColumns}
      createHref="/settings/members/new"
      detailHref={(row) => `/settings/members/${String(row.membershipId)}`}
      canCreate={isManager(identity)}
    />
  );
}

export function TaskTemplatesPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("taskTemplates")}
      description="Reusable definitions. Editing a template never changes historical task snapshots."
      endpoint="/task-templates"
      queryKey={queryKeys.taskTemplates.lists()}
      columns={taskTemplateColumns}
      createHref="/task-templates/new"
      detailHref={(row) => `/task-templates/${String(row.id)}`}
      canCreate={isManager(identity)}
    />
  );
}

export function TaskSchedulesPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("taskSchedules")}
      description="Schedules generate independent task instances in the backend's bounded window."
      endpoint="/task-schedules"
      queryKey={queryKeys.taskSchedules.lists()}
      columns={taskScheduleColumns}
      createHref="/task-schedules/new"
      detailHref={(row) => `/task-schedules/${String(row.id)}/edit`}
      canCreate={isManager(identity)}
      canDelete={isManager(identity)}
    />
  );
}

export function TasksPage({ mine = false }: { mine?: boolean }) {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={mine ? t("myTasks") : t("tasks")}
      description={
        mine
          ? "Tasks assigned to you or visible through your department scope."
          : "Task instances are execution snapshots; filters and authorization are enforced by the API."
      }
      endpoint={mine ? "/tasks/my" : "/tasks"}
      queryKey={queryKeys.tasks.lists({ mine })}
      columns={taskColumns}
      createHref={mine ? undefined : "/tasks/new"}
      detailHref={(row) => `/tasks/${String(row.id)}`}
      canDelete={!mine && isSupervisorOrManager(identity)}
    />
  );
}

export function OrderTemplatesPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("orderTemplates")}
      description="Define selectable items between two different departments; no stock is tracked."
      endpoint="/order-templates"
      queryKey={queryKeys.orderTemplates.lists()}
      columns={orderTemplateColumns}
      createHref="/order-templates/new"
      detailHref={(row) => `/order-templates/${String(row.id)}`}
      canCreate={isManager(identity)}
    />
  );
}

export function OrdersPage({
  scope = "all",
}: {
  scope?: "all" | "incoming" | "outgoing";
}) {
  const t = useTranslations("Pages");
  const endpoint =
    scope === "all" ? "/department-orders" : `/department-orders/${scope}`;
  return (
    <CollectionPage
      title={
        scope === "incoming"
          ? t("incomingOrders")
          : scope === "outgoing"
            ? t("outgoingOrders")
            : t("orders")
      }
      description="Ready, delivered, and received are separate workflow states."
      endpoint={endpoint}
      queryKey={queryKeys.orders.lists(scope)}
      columns={orderColumns}
      createHref={scope === "incoming" ? undefined : "/department-orders/new"}
      detailHref={(row) => `/department-orders/${String(row.id)}`}
    />
  );
}

export function ComplaintsPage() {
  const t = useTranslations("Pages");
  return (
    <CollectionPage
      title={t("complaints")}
      description="Only complaints and messages authorized by the backend are returned."
      endpoint="/complaints"
      queryKey={queryKeys.complaints.lists()}
      columns={complaintColumns}
      createHref="/complaints/new"
      detailHref={(row) => `/complaints/${String(row.id)}`}
    />
  );
}

export function NotificationsPage() {
  const t = useTranslations("Pages");
  return (
    <CollectionPage
      title={t("notifications")}
      endpoint="/notifications"
      queryKey={queryKeys.notifications.lists()}
      columns={[
        { key: "title", label: "Title" },
        { key: "body", label: "Message" },
        { key: "isRead", label: "Read" },
        { key: "createdAt", label: "Created", dateTime: true },
      ]}
    />
  );
}

export function PlatformOrganizationsPage() {
  const t = useTranslations("Pages");
  return (
    <CollectionPage
      title={t("platformOrganizations")}
      endpoint="/platform/organizations"
      queryKey={queryKeys.platform.organizations()}
      realm="platform"
      columns={[
        { key: "name", label: "Name" },
        {
          key: "status",
          label: "Organization",
          enumKind: "organizationStatus",
        },
        {
          key: "subscriptionStatus",
          label: "Subscription",
          enumKind: "subscriptionStatus",
        },
        { key: "subscriptionEndsAt", label: "Ends", dateTime: true },
      ]}
      detailHref={(row) => `/platform/organizations/${String(row.id)}`}
    />
  );
}

export function PlatformPlansPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("platformPlans")}
      endpoint="/platform/subscription-plans"
      queryKey={queryKeys.platform.plans()}
      realm="platform"
      columns={[
        { key: "name", label: "Name" },
        { key: "code", label: "Code" },
        { key: "currency", label: "Currency" },
        { key: "maxUsers", label: "Users" },
        { key: "maxBranches", label: "Branches" },
        { key: "isActive", label: "Active" },
      ]}
      createHref="/platform/plans/new"
      detailHref={(row) => `/platform/plans/${String(row.id)}`}
      canCreate={isPlatformAdministrator(identity)}
    />
  );
}

export function PlatformPaymentsPage() {
  const t = useTranslations("Pages");
  const { identity } = useAuth();
  return (
    <CollectionPage
      title={t("platformPayments")}
      endpoint="/platform/manual-payments"
      queryKey={queryKeys.platform.payments()}
      realm="platform"
      columns={[
        { key: "organizationId", label: "Organization" },
        { key: "amount", label: "Amount" },
        { key: "currency", label: "Currency" },
        { key: "paymentMethod", label: "Method", enumKind: "paymentMethod" },
        { key: "status", label: "Status", enumKind: "paymentStatus" },
        { key: "paidAt", label: "Paid", dateTime: true },
      ]}
      createHref="/platform/payments/new"
      detailHref={(row) => `/platform/payments/${String(row.id)}`}
      canCreate={isPlatformAdministrator(identity)}
    />
  );
}
