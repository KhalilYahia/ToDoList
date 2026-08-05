"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Button,
  Card,
  Dialog,
  Field,
  Input,
  Skeleton,
  Textarea,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import { branchSchema, type BranchValues } from "@/lib/forms/validation";
import { isPlatformAdministrator } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";

import { CollectionPage, type Column } from "../shared/collection-page";

const branchColumns: Column[] = [
  { key: "name", label: "Name" },
  { key: "timezone", label: "Timezone" },
  { key: "isPrimary", label: "Primary" },
  { key: "isActive", label: "Active" },
];

function branchesPath(organizationId: string): string {
  return `/platform/organizations/${organizationId}/branches`;
}

async function findBranch(
  organizationId: string,
  branchId: string,
  signal?: AbortSignal,
): Promise<Schemas["BranchDto"]> {
  const pageSize = 200;
  let page = 1;

  while (true) {
    const response = await apiRequest<PagedResponse<Schemas["BranchDto"]>>(
      `${branchesPath(organizationId)}?page=${page}&pageSize=${pageSize}`,
      { realm: "platform", signal },
    );
    const branch = response.items.find((item) => item.id === branchId);
    if (branch) return branch;
    if (page * pageSize >= Number(response.totalCount)) break;
    page += 1;
  }

  throw new Error("Branch not found in this organization.");
}

function AdministratorRequired() {
  return (
    <>
      <PageHeader
        title="Organization branches"
        description="Branch management is restricted to platform administrators."
      />
      <Alert tone="danger">
        Only platform administrators can view or change an organization&apos;s
        branches.
      </Alert>
    </>
  );
}

export function PlatformBranchesPage({
  organizationId,
}: {
  organizationId: string;
}) {
  const { identity } = useAuth();

  if (!isPlatformAdministrator(identity)) return <AdministratorRequired />;

  return (
    <CollectionPage
      title="Organization branches"
      description="Add, update, and remove branches for this organization. Active branches are limited by its subscription plan."
      endpoint={branchesPath(organizationId)}
      queryKey={queryKeys.platform.organizationBranches(organizationId)}
      realm="platform"
      columns={branchColumns}
      createHref={`/platform/organizations/${organizationId}/branches/new`}
      detailHref={(row) =>
        `/platform/organizations/${organizationId}/branches/${String(row.id)}`
      }
      extraActions={
        <Button variant="secondary">
          <Link href={`/platform/organizations/${organizationId}`}>
            Organization
          </Link>
        </Button>
      }
    />
  );
}

export function PlatformBranchForm({
  organizationId,
  branchId,
}: {
  organizationId: string;
  branchId?: string;
}) {
  const { identity } = useAuth();
  const administrator = isPlatformAdministrator(identity);
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const listPath = branchesPath(organizationId);
  const query = useQuery({
    queryKey: branchId
      ? queryKeys.platform.organizationBranch(organizationId, branchId)
      : [...queryKeys.platform.organizationBranches(organizationId), "new"],
    queryFn: ({ signal }) => findBranch(organizationId, branchId!, signal),
    enabled: administrator && Boolean(branchId),
  });
  const form = useForm<BranchValues>({
    resolver: zodResolver(branchSchema),
    defaultValues: {
      name: "",
      address: "",
      phone: "",
      timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
      isPrimary: false,
      isActive: true,
    },
  });

  useEffect(() => {
    if (!query.data) return;
    form.reset({
      name: query.data.name,
      address: query.data.address ?? "",
      phone: query.data.phone ?? "",
      timezone: query.data.timezone,
      isPrimary: query.data.isPrimary,
      isActive: query.data.isActive,
    });
  }, [form, query.data]);

  const saveMutation = useMutation({
    mutationFn: (values: BranchValues) => {
      const body: Schemas["SaveBranchRequest"] = {
        ...values,
        address: values.address || null,
        phone: values.phone || null,
      };
      return apiRequest<Schemas["BranchDto"]>(
        branchId ? `${listPath}/${branchId}` : listPath,
        {
          method: branchId ? "PATCH" : "POST",
          body,
          realm: "platform",
        },
      );
    },
    onSuccess: (branch) => {
      queryClient.setQueryData(
        queryKeys.platform.organizationBranch(organizationId, branch.id),
        branch,
      );
      void queryClient.invalidateQueries({
        queryKey: queryKeys.platform.organizationBranches(organizationId),
      });
      toast.push(branchId ? "Branch updated." : "Branch created.");
      router.push(`/platform/organizations/${organizationId}/branches`);
    },
  });
  const deleteMutation = useMutation({
    mutationFn: () =>
      apiRequest<void>(`${listPath}/${branchId}`, {
        method: "DELETE",
        realm: "platform",
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.platform.organizationBranches(organizationId),
      });
      toast.push("Branch deleted.");
      setDeleteDialogOpen(false);
      router.push(`/platform/organizations/${organizationId}/branches`);
    },
  });

  if (!administrator) return <AdministratorRequired />;
  if (branchId && query.isLoading) return <Skeleton className="h-[30rem]" />;

  return (
    <>
      <PageHeader
        title={branchId ? "Edit branch" : "Add branch"}
        description="Changes apply to the selected organization and are audited by the platform backend."
        actions={
          <>
            <Button variant="secondary">
              <Link href={`/platform/organizations/${organizationId}/branches`}>
                Back to branches
              </Link>
            </Button>
            {branchId ? (
              <Button
                variant="danger"
                onClick={() => setDeleteDialogOpen(true)}
              >
                Delete branch
              </Button>
            ) : null}
          </>
        }
      />
      <Card className="max-w-3xl">
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}
        >
          {query.error || saveMutation.error || deleteMutation.error ? (
            <div className="md:col-span-2">
              <Alert tone="danger">
                {errorMessage(
                  query.error ?? saveMutation.error ?? deleteMutation.error,
                )}
              </Alert>
            </div>
          ) : null}
          <Field
            label="Name"
            error={form.formState.errors.name?.message}
            required
          >
            <Input {...form.register("name")} />
          </Field>
          <Field
            label="Timezone"
            error={form.formState.errors.timezone?.message}
            required
          >
            <Input {...form.register("timezone")} />
          </Field>
          <Field label="Phone" error={form.formState.errors.phone?.message}>
            <Input type="tel" {...form.register("phone")} />
          </Field>
          <div className="flex items-center gap-6 pt-7">
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" {...form.register("isPrimary")} /> Primary
            </label>
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" {...form.register("isActive")} /> Active
            </label>
          </div>
          <div className="md:col-span-2">
            <Field
              label="Address"
              error={form.formState.errors.address?.message}
            >
              <Textarea {...form.register("address")} />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Button type="submit" busy={saveMutation.isPending}>
              {branchId ? "Save branch" : "Add branch"}
            </Button>
          </div>
        </form>
      </Card>
      <Dialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        title="Delete branch?"
      >
        <p className="text-ink-600 text-sm">
          This action is blocked if the branch has departments or is the
          organization&apos;s only active branch.
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
            Delete branch
          </Button>
        </div>
      </Dialog>
    </>
  );
}
