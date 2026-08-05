"use client";

import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  Dialog,
  Field,
  Input,
  Select,
  Skeleton,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode, enumCodes, enumValue, statusTone } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import { fetchAllPages } from "@/lib/api/pagination";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  createMemberSchema,
  memberSchema,
  type MemberValues,
} from "@/lib/forms/validation";
import { canMutateTenant, isManager } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";

type DepartmentGroup = {
  branchId: string;
  branchName: string;
  departments: Schemas["DepartmentDto"][];
};

export function MemberForm({ id }: { id?: string }) {
  const { identity } = useAuth();
  const manager = isManager(identity);
  const writable = manager && canMutateTenant(identity);
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [suspendDialogOpen, setSuspendDialogOpen] = useState(false);
  const member = useQuery({
    queryKey: id
      ? queryKeys.organization.member(id)
      : [...queryKeys.organization.membersRoot, "new"],
    queryFn: () => apiRequest<Schemas["MemberDto"]>(`/members/${id}`),
    enabled: Boolean(id),
  });
  const branches = useQuery({
    queryKey: queryKeys.organization.branches({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["BranchDto"]>("/branches", { signal }),
  });
  const departments = useQuery({
    queryKey: queryKeys.organization.departments({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["DepartmentDto"]>("/departments", { signal }),
  });
  const form = useForm<MemberValues>({
    resolver: zodResolver(id ? memberSchema : createMemberSchema),
    defaultValues: {
      fullName: "",
      email: "",
      phone: "",
      preferredLanguage: "en",
      role: "Employee",
      temporaryPassword: "",
      departmentIds: [],
    },
  });

  useEffect(() => {
    if (!member.data) return;
    form.reset({
      fullName: member.data.fullName,
      email: member.data.email ?? "",
      phone: member.data.phone ?? "",
      preferredLanguage: "en",
      role: enumCode(
        "organizationRole",
        member.data.role,
      ) as MemberValues["role"],
      temporaryPassword: "",
      departmentIds: member.data.departmentIds,
    });
  }, [form, member.data]);

  const departmentGroups = useMemo<DepartmentGroup[]>(() => {
    const branchNames = new Map(
      (branches.data ?? []).map((branch) => [branch.id, branch.name]),
    );
    const grouped = new Map<string, Schemas["DepartmentDto"][]>();
    for (const department of departments.data ?? []) {
      const existing = grouped.get(department.branchId) ?? [];
      existing.push(department);
      grouped.set(department.branchId, existing);
    }
    return [...grouped.entries()]
      .map(([branchId, groupedDepartments]) => ({
        branchId,
        branchName: branchNames.get(branchId) ?? "Unknown branch",
        departments: groupedDepartments.sort((left, right) =>
          left.name.localeCompare(right.name),
        ),
      }))
      .sort((left, right) => left.branchName.localeCompare(right.branchName));
  }, [branches.data, departments.data]);

  const saveMutation = useMutation({
    mutationFn: async (values: MemberValues) => {
      if (!id) {
        return apiRequest<Schemas["MemberDto"]>("/members", {
          method: "POST",
          body: {
            fullName: values.fullName,
            email: values.email,
            phone: values.phone || null,
            preferredLanguage: values.preferredLanguage,
            role: enumValue("organizationRole", values.role),
            temporaryPassword: values.temporaryPassword,
            departmentIds: values.departmentIds,
          } satisfies Schemas["CreateMemberRequest"],
        });
      }

      await apiRequest<Schemas["MemberDto"]>(`/members/${id}`, {
        method: "PATCH",
        body: {
          fullName: values.fullName,
          phone: values.phone || null,
          preferredLanguage: values.preferredLanguage,
          role: enumValue("organizationRole", values.role),
        } satisfies Schemas["UpdateMemberRequest"],
      });
      await apiRequest<void>(`/members/${id}/departments`, {
        method: "PUT",
        body: { departmentIds: values.departmentIds },
      });
      return apiRequest<Schemas["MemberDto"]>(`/members/${id}`);
    },
    onSuccess: (savedMember) => {
      queryClient.setQueryData(
        queryKeys.organization.member(savedMember.membershipId),
        savedMember,
      );
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organization.membersRoot,
      });
      toast.push(id ? "Member updated." : "Member created.");
      if (!id) router.push(`/settings/members/${savedMember.membershipId}`);
    },
  });
  const stateMutation = useMutation({
    mutationFn: (action: "activate" | "suspend") =>
      apiRequest<void>(`/members/${id}/${action}`, { method: "POST" }),
    onSuccess: async (_, action) => {
      await member.refetch();
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organization.membersRoot,
      });
      setSuspendDialogOpen(false);
      toast.push(
        action === "activate" ? "Member activated." : "Member suspended.",
      );
    },
  });

  if (!manager && !id) {
    return (
      <Alert tone="danger">
        Only organization managers can create members.
      </Alert>
    );
  }
  if (id && member.isLoading) return <Skeleton className="h-[34rem]" />;
  if (id && (member.error || !member.data)) {
    return <Alert tone="danger">{errorMessage(member.error)}</Alert>;
  }

  const role = member.data
    ? enumCode("organizationRole", member.data.role)
    : null;
  const accountStatus = member.data
    ? enumCode("accountStatus", member.data.accountStatus)
    : null;

  return (
    <>
      <PageHeader
        title={
          id ? (member.data?.fullName ?? "Member details") : "Create member"
        }
        description="A member has one organization-wide role and can belong to multiple departments."
        actions={
          <>
            <Button variant="secondary">
              <Link href="/settings/members">Back to members</Link>
            </Button>
            {role ? <Badge>{role}</Badge> : null}
            {accountStatus ? (
              <Badge tone={statusTone(accountStatus)}>{accountStatus}</Badge>
            ) : null}
            {id && writable && member.data?.isActive === false ? (
              <Button
                busy={
                  stateMutation.isPending &&
                  stateMutation.variables === "activate"
                }
                onClick={() => stateMutation.mutate("activate")}
              >
                Activate member
              </Button>
            ) : null}
            {id && writable && member.data?.isActive ? (
              <Button
                variant="danger"
                onClick={() => setSuspendDialogOpen(true)}
              >
                Suspend member
              </Button>
            ) : null}
          </>
        }
      />
      {!manager ? (
        <Alert tone="info">
          You can view this member, but only a manager can change their role,
          departments, or membership state.
        </Alert>
      ) : !writable ? (
        <Alert tone="warning">
          The organization&apos;s subscription currently allows read-only
          access.
        </Alert>
      ) : null}
      {member.data && !member.data.isActive ? (
        <div className="mt-5">
          <Alert tone="warning">
            This member is suspended from the organization.
          </Alert>
        </div>
      ) : null}
      <Card className="mt-5 max-w-5xl">
        <form
          className="grid gap-5"
          onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}
        >
          {branches.error ||
          departments.error ||
          saveMutation.error ||
          stateMutation.error ? (
            <Alert tone="danger">
              {errorMessage(
                branches.error ??
                  departments.error ??
                  saveMutation.error ??
                  stateMutation.error,
              )}
            </Alert>
          ) : null}
          <div className="grid gap-4 md:grid-cols-2">
            <Field
              label="Full name"
              error={form.formState.errors.fullName?.message}
              required
            >
              <Input disabled={!writable} {...form.register("fullName")} />
            </Field>
            <Field
              label="Email"
              error={form.formState.errors.email?.message}
              required={!id}
            >
              <Input
                type="email"
                disabled={!writable || Boolean(id)}
                {...form.register("email")}
              />
            </Field>
            <Field label="Phone" error={form.formState.errors.phone?.message}>
              <Input
                type="tel"
                disabled={!writable}
                {...form.register("phone")}
              />
            </Field>
            <Field label="Preferred UI language" required>
              <Select
                disabled={!writable}
                {...form.register("preferredLanguage")}
              >
                <option value="en">English</option>
                <option value="ru">Русский</option>
                <option value="ar">العربية</option>
              </Select>
            </Field>
            <Field
              label="Organization role"
              error={form.formState.errors.role?.message}
              required
            >
              <Select disabled={!writable} {...form.register("role")}>
                {enumCodes.organizationRole.map((organizationRole) => (
                  <option key={organizationRole} value={organizationRole}>
                    {organizationRole}
                  </option>
                ))}
              </Select>
            </Field>
            {!id ? (
              <Field
                label="Temporary password"
                hint="At least 12 characters with upper-case, lower-case, and numeric characters."
                error={form.formState.errors.temporaryPassword?.message}
                required
              >
                <Input
                  type="password"
                  autoComplete="new-password"
                  disabled={!writable}
                  {...form.register("temporaryPassword")}
                />
              </Field>
            ) : null}
          </div>
          <fieldset disabled={!writable} >
            <legend className="text-sm font-bold">Department access</legend>
            <p className="text-ink-600 mt-1 text-xs">
              Select every department this member should belong to.
            </p>
            {departments.isLoading || branches.isLoading ? (
              <Skeleton className="mt-3 h-32" />
            ) : departmentGroups.length === 0 ? (
              <Alert tone="info">
                Create a department before assigning department access.
              </Alert>
            ) : (
              <div className="mt-3 grid gap-3 md:grid-cols-2">
                {departmentGroups.map((group) => (
                  <div
                    key={group.branchId}
                    className="border-ink-950/10 rounded-xl border p-4"
                  >
                    <p className="font-bold">{group.branchName}</p>
                    <div className="mt-3 grid gap-2">
                      {group.departments.map((department) => (
                        <label
                          key={department.id}
                          className="flex items-center gap-2 text-sm"
                        >
                          <input
                            type="checkbox"
                            value={department.id}
                            {...form.register("departmentIds")}
                          />
                          <span>{department.name}</span>
                          {!department.isActive ? (
                            <Badge tone="warning">Inactive</Badge>
                          ) : null}
                        </label>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </fieldset>
          {writable ? (
            <div>
              <Button type="submit" busy={saveMutation.isPending}>
                {id ? "Save member" : "Create member"}
              </Button>
            </div>
          ) : null}
        </form>
      </Card>
      <Dialog
        open={suspendDialogOpen}
        onClose={() => setSuspendDialogOpen(false)}
        title="Suspend member?"
      >
        <p className="text-ink-600 text-sm">
          The member will lose organization access. The backend prevents
          suspending the last active manager.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <Button
            variant="secondary"
            onClick={() => setSuspendDialogOpen(false)}
          >
            Cancel
          </Button>
          <Button
            variant="danger"
            busy={
              stateMutation.isPending && stateMutation.variables === "suspend"
            }
            onClick={() => stateMutation.mutate("suspend")}
          >
            Suspend member
          </Button>
        </div>
      </Dialog>
      {id && manager ? <ResetMemberPasswordCard membershipId={id} /> : null}
    </>
  );
}

function ResetMemberPasswordCard({ membershipId }: { membershipId: string }) {
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();

  const resetMutation = useMutation({
    mutationFn: () =>
      apiRequest(`/members/${membershipId}/reset-password`, {
        method: "POST",
        body: { newPassword },
      }),
    onSuccess: () => {
      toast.push("Member password has been reset successfully", "success");
      setNewPassword("");
      setConfirmPassword("");
      setError(null);
    },
    onError: (err) => {
      setError(errorMessage(err));
    },
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (newPassword.length < 8) {
      setError("New password must be at least 8 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }
    resetMutation.mutate();
  }

  return (
    <Card className="mt-5 max-w-5xl">
      <h2 className="text-lg font-black">Reset Member Password</h2>
      <p className="text-ink-600 mt-1 text-sm">
        As an organization manager, you can set a new password for this user.
      </p>
      <form onSubmit={handleSubmit} className="mt-4 grid gap-4 md:grid-cols-2">
        {error ? (
          <div className="md:col-span-2">
            <Alert tone="danger">{error}</Alert>
          </div>
        ) : null}
        <Field 
          label="New password" hint="At least 8 characters" required>
          <Input
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
        </Field>
        <Field         
          style={{ alignContent: "start" }}
          label="Confirm new password" required>
          <Input
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
          />
        </Field>
        <div className="md:col-span-2">
          <Button type="submit" disabled={resetMutation.isPending}>
            {resetMutation.isPending ? "Resetting..." : "Reset Password"}
          </Button>
        </div>
      </form>
    </Card>
  );
}
