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
  EmptyState,
  Field,
  Input,
  Select,
  Skeleton,
  Textarea,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import { fetchAllPages } from "@/lib/api/pagination";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  departmentSchema,
  type DepartmentValues,
} from "@/lib/forms/validation";
import { canMutateTenant, isManager } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";

function memberRole(member: Schemas["MemberDto"]): string {
  return enumCode("organizationRole", member.role);
}

function DepartmentMembers({
  departmentId,
  members,
  writable,
  isLoading,
  error,
}: {
  departmentId: string;
  members: Schemas["MemberDto"][];
  writable: boolean;
  isLoading: boolean;
  error: unknown;
}) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [membershipId, setMembershipId] = useState("");
  const assignedMembers = useMemo(
    () =>
      members
        .filter((member) => member.departmentIds.includes(departmentId))
        .sort((left, right) => left.fullName.localeCompare(right.fullName)),
    [departmentId, members],
  );
  const availableMembers = useMemo(
    () =>
      members
        .filter((member) => !member.departmentIds.includes(departmentId))
        .sort((left, right) => left.fullName.localeCompare(right.fullName)),
    [departmentId, members],
  );
  const assignmentMutation = useMutation({
    mutationFn: ({
      member,
      assign,
    }: {
      member: Schemas["MemberDto"];
      assign: boolean;
    }) => {
      const departmentIds = assign
        ? [...new Set([...member.departmentIds, departmentId])]
        : member.departmentIds.filter((id) => id !== departmentId);
      return apiRequest<void>(`/members/${member.membershipId}/departments`, {
        method: "PUT",
        body: { departmentIds },
      });
    },
    onSuccess: (_, variables) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organization.membersRoot,
      });
      if (variables.assign) setMembershipId("");
      toast.push(
        variables.assign
          ? "Member assigned to department."
          : "Member removed from department.",
      );
    },
  });

  function assignSelectedMember() {
    const member = availableMembers.find(
      (candidate) => candidate.membershipId === membershipId,
    );
    if (member) assignmentMutation.mutate({ member, assign: true });
  }

  return (
    <Card>
      <div className="mb-4">
        <h2 className="text-lg font-black">Department members</h2>
        <p className="text-ink-600 mt-1 text-sm">
          Roles apply across the organization. Open a member to change their
          Manager, Supervisor, or Employee role.
        </p>
      </div>
      {error || assignmentMutation.error ? (
        <Alert tone="danger">
          {errorMessage(error ?? assignmentMutation.error)}
        </Alert>
      ) : null}
      {writable ? (
        <div className="mb-5 grid gap-3 sm:grid-cols-[1fr_auto]">
          <Field label="Assign existing member">
            <Select
              value={membershipId}
              disabled={isLoading || availableMembers.length === 0}
              onChange={(event) => setMembershipId(event.target.value)}
            >
              <option value="">Select a member</option>
              {availableMembers.map((member) => (
                <option key={member.membershipId} value={member.membershipId}>
                  {member.fullName} — {memberRole(member)}
                </option>
              ))}
            </Select>
          </Field>
          <Button
            className="self-end"
            disabled={!membershipId}
            busy={assignmentMutation.isPending}
            onClick={assignSelectedMember}
          >
            Assign member
          </Button>
        </div>
      ) : null}
      {isLoading ? (
        <Skeleton className="h-40" />
      ) : assignedMembers.length === 0 ? (
        <EmptyState
          title="No assigned members"
          description="Assign an organization member to make them part of this department."
        />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-start text-sm">
            <thead>
              <tr className="border-ink-950/10 border-b">
                <th className="text-ink-600 px-3 py-2 text-start text-xs font-bold uppercase">
                  Member
                </th>
                <th className="text-ink-600 px-3 py-2 text-start text-xs font-bold uppercase">
                  Role
                </th>
                <th className="text-ink-600 px-3 py-2 text-start text-xs font-bold uppercase">
                  Membership
                </th>
                <th className="px-3 py-2">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {assignedMembers.map((member) => (
                <tr
                  key={member.membershipId}
                  className="border-ink-950/7 border-b last:border-0"
                >
                  <td className="px-3 py-3">
                    <p className="font-semibold">{member.fullName}</p>
                    <p className="text-ink-600 text-xs">
                      {member.email ?? "No email"}
                    </p>
                  </td>
                  <td className="px-3 py-3">
                    <Badge>{memberRole(member)}</Badge>
                  </td>
                  <td className="px-3 py-3">
                    <Badge tone={member.isActive ? "success" : "danger"}>
                      {member.isActive ? "Active" : "Suspended"}
                    </Badge>
                  </td>
                  <td className="px-3 py-3 text-end">
                    <div className="flex justify-end gap-3">
                      <Link
                        href={`/settings/members/${member.membershipId}`}
                        className="text-brand-700 font-semibold hover:underline"
                      >
                        View member
                      </Link>
                      {writable ? (
                        <button
                          type="button"
                          className="text-danger-700 font-semibold hover:underline disabled:opacity-50"
                          disabled={assignmentMutation.isPending}
                          onClick={() =>
                            assignmentMutation.mutate({
                              member,
                              assign: false,
                            })
                          }
                        >
                          Remove
                        </button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

export function DepartmentForm({ id }: { id?: string }) {
  const { identity } = useAuth();
  const manager = isManager(identity);
  const writable = manager && canMutateTenant(identity);
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const branches = useQuery({
    queryKey: queryKeys.organization.branches({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["BranchDto"]>("/branches", { signal }),
  });
  const members = useQuery({
    queryKey: queryKeys.organization.members({ scope: "all" }),
    queryFn: ({ signal }) =>
      fetchAllPages<Schemas["MemberDto"]>("/members", { signal }),
    retry: false,
  });
  const department = useQuery({
    queryKey: id
      ? queryKeys.organization.department(id)
      : [...queryKeys.organization.departmentsRoot, "new"],
    queryFn: () => apiRequest<Schemas["DepartmentDto"]>(`/departments/${id}`),
    enabled: Boolean(id),
  });
  const form = useForm<DepartmentValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: {
      branchId: "",
      name: "",
      description: "",
      supervisorUserId: "",
      isActive: true,
    },
  });

  useEffect(() => {
    if (!department.data) return;
    form.reset({
      branchId: department.data.branchId,
      name: department.data.name,
      description: department.data.description ?? "",
      supervisorUserId: department.data.supervisorUserId ?? "",
      isActive: department.data.isActive,
    });
  }, [department.data, form]);

  const saveMutation = useMutation({
    mutationFn: (values: DepartmentValues) =>
      apiRequest<Schemas["DepartmentDto"]>(
        id ? `/departments/${id}` : "/departments",
        {
          method: id ? "PATCH" : "POST",
          body: {
            ...values,
            description: values.description || null,
            supervisorUserId: values.supervisorUserId || null,
          } satisfies Schemas["SaveDepartmentRequest"],
        },
      ),
    onSuccess: (savedDepartment) => {
      queryClient.setQueryData(
        queryKeys.organization.department(savedDepartment.id),
        savedDepartment,
      );
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organization.departmentsRoot,
      });
      toast.push(id ? "Department updated." : "Department created.");
      if (!id) {
        router.push(`/settings/departments/${savedDepartment.id}`);
      }
    },
  });
  const deleteMutation = useMutation({
    mutationFn: () =>
      apiRequest<void>(`/departments/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.organization.departmentsRoot,
      });
      toast.push("Department deleted.");
      setDeleteDialogOpen(false);
      router.push("/settings/departments");
    },
  });

  const supervisors = useMemo(
    () =>
      (members.data ?? []).filter(
        (member) =>
          member.isActive &&
          ["Manager", "Supervisor"].includes(memberRole(member)),
      ),
    [members.data],
  );

  if (!manager && !id) {
    return (
      <Alert tone="danger">
        Only organization managers can create departments.
      </Alert>
    );
  }
  if (id && department.isLoading) return <Skeleton className="h-[30rem]" />;
  if (id && (department.error || !department.data)) {
    return <Alert tone="danger">{errorMessage(department.error)}</Alert>;
  }

  return (
    <>
      <PageHeader
        title={id ? "Department details" : "Create department"}
        description="Departments belong to a branch. A supervisor must be an active Manager or Supervisor."
        actions={
          <>
            <Button variant="secondary">
              <Link href="/settings/departments">Back to departments</Link>
            </Button>
            {id && writable ? (
              <Button
                variant="danger"
                onClick={() => setDeleteDialogOpen(true)}
              >
                Delete department
              </Button>
            ) : null}
          </>
        }
      />
      {!manager ? (
        <Alert tone="info">
          You can view this department, but only a manager can change it or
          assign members.
        </Alert>
      ) : !writable ? (
        <Alert tone="warning">
          The organization&apos;s subscription currently allows read-only
          access.
        </Alert>
      ) : null}
      <div className="mt-5 grid max-w-5xl gap-5">
        <Card>
          <form
            className="grid gap-4 md:grid-cols-2"
            onSubmit={form.handleSubmit((values) =>
              saveMutation.mutate(values),
            )}
          >
            {branches.error ||
            members.error ||
            saveMutation.error ||
            deleteMutation.error ? (
              <div className="md:col-span-2">
                <Alert tone="danger">
                  {errorMessage(
                    branches.error ??
                      members.error ??
                      saveMutation.error ??
                      deleteMutation.error,
                  )}
                </Alert>
              </div>
            ) : null}
            <Field
              label="Branch"
              error={form.formState.errors.branchId?.message}
              required
            >
              <Select disabled={!writable} {...form.register("branchId")}>
                <option value="">Select branch</option>
                {branches.data?.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field
              label="Name"
              error={form.formState.errors.name?.message}
              required
            >
              <Input disabled={!writable} {...form.register("name")} />
            </Field>
            <Field label="Supervisor">
              <Select
                disabled={!writable}
                {...form.register("supervisorUserId")}
              >
                <option value="">No supervisor</option>
                {supervisors.map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.fullName} — {memberRole(member)}
                  </option>
                ))}
              </Select>
            </Field>
            <label className="flex items-center gap-2 pt-7 text-sm font-semibold">
              <input
                type="checkbox"
                disabled={!writable}
                {...form.register("isActive")}
              />{" "}
              Active
            </label>
            <div className="md:col-span-2">
              <Field
                label="Description"
                error={form.formState.errors.description?.message}
              >
                <Textarea
                  disabled={!writable}
                  {...form.register("description")}
                />
              </Field>
            </div>
            {writable ? (
              <div className="md:col-span-2">
                <Button type="submit" busy={saveMutation.isPending}>
                  {id ? "Save department" : "Create department"}
                </Button>
              </div>
            ) : null}
          </form>
        </Card>
        {id ? (
          <DepartmentMembers
            departmentId={id}
            members={members.data ?? []}
            writable={writable}
            isLoading={members.isLoading}
            error={members.error}
          />
        ) : null}
      </div>
      <Dialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        title="Delete department?"
      >
        <p className="text-ink-600 text-sm">
          Departments with tasks, templates, or order history cannot be deleted.
          Member assignments are managed separately.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <Button
            variant="secondary"
            onClick={() => setDeleteDialogOpen(false)}
          >
            Cancel
          </Button>
          <Button
            variant="danger"
            busy={deleteMutation.isPending}
            onClick={() => deleteMutation.mutate()}
          >
            Delete department
          </Button>
        </div>
      </Dialog>
    </>
  );
}
