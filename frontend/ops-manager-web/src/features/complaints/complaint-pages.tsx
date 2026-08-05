"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm, useWatch } from "react-hook-form";
import { useLocale } from "next-intl";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  FileUploader,
  Select,
  Skeleton,
  Textarea,
  Input,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode, enumCodes, enumValue, statusTone } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import { complaintSchema, type ComplaintValues } from "@/lib/forms/validation";
import {
  canMutateTenant,
  isSupervisorOrManager,
} from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";
import { formatDateTime } from "@/lib/utils";

import { DetailGrid } from "../shared/detail-grid";
import { useReferenceData } from "../shared/reference-data";

export function ComplaintForm() {
  const references = useReferenceData();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const form = useForm<ComplaintValues>({
    resolver: zodResolver(complaintSchema),
    defaultValues: {
      branchId: "",
      targetDepartmentId: "",
      title: "",
      description: "",
      visibility: "Participants",
    },
  });
  const branchId = useWatch({ control: form.control, name: "branchId" });
  const mutation = useMutation({
    mutationFn: (values: ComplaintValues) =>
      apiRequest<Schemas["ComplaintDto"]>("/complaints", {
        method: "POST",
        body: {
          branchId: values.branchId,
          targetDepartmentId: values.targetDepartmentId || null,
          title: values.title.trim(),
          description: values.description.trim(),
          visibility: enumValue("complaintVisibility", values.visibility),
        },
      }),
    onSuccess: (complaint) => {
      void queryClient.invalidateQueries({ queryKey: ["complaints"] });
      toast.push("Complaint submitted.");
      router.push(`/complaints/${complaint.id}`);
    },
  });

  return (
    <>
      <PageHeader
        title="New complaint"
        description="Submit an issue to management or a specific department."
      />
      <form
        className="grid max-w-3xl gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {mutation.error ? (
          <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field
            label="Branch"
            required
            error={form.formState.errors.branchId?.message}
          >
            <Select {...form.register("branchId")}>
              <option value="">Select branch</option>
              {references.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            label="Target department"
            hint="Optional. Leave blank for management."
          >
            <Select {...form.register("targetDepartmentId")}>
              <option value="">Management</option>
              {references.departments
                .filter(
                  (department) => !branchId || department.branchId === branchId,
                )
                .map((department) => (
                  <option key={department.id} value={department.id}>
                    {department.name}
                  </option>
                ))}
            </Select>
          </Field>
          <Field
            label="Title"
            required
            error={form.formState.errors.title?.message}
          >
            <Input {...form.register("title")} />
          </Field>
          <Field
            label="Visibility"
            required
            error={form.formState.errors.visibility?.message}
          >
            <Select {...form.register("visibility")}>
              {enumCodes.complaintVisibility.map((visibility) => (
                <option key={visibility}>{visibility}</option>
              ))}
            </Select>
          </Field>
          <div className="md:col-span-2">
            <Field
              label="Description"
              required
              error={form.formState.errors.description?.message}
            >
              <Textarea {...form.register("description")} />
            </Field>
          </div>
        </Card>
        <div>
          <Button type="submit" busy={mutation.isPending}>
            Submit complaint
          </Button>
        </div>
      </form>
    </>
  );
}

export function ComplaintDetail({ id }: { id: string }) {
  const locale = useLocale();
  const { identity } = useAuth();
  const references = useReferenceData();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [message, setMessage] = useState("");
  const [internal, setInternal] = useState(false);
  const [assignee, setAssignee] = useState("");
  const [actionError, setActionError] = useState<string>();
  const query = useQuery({
    queryKey: queryKeys.complaints.detail(id),
    queryFn: () => apiRequest<Schemas["ComplaintDto"]>(`/complaints/${id}`),
  });
  const actionMutation = useMutation({
    mutationFn: ({
      action,
      body,
    }: {
      action: string;
      body?: Record<string, unknown>;
    }) =>
      apiRequest<unknown>(`/complaints/${id}/${action}`, {
        method: "POST",
        body,
      }),
    onSuccess: () => {
      setMessage("");
      setInternal(false);
      void query.refetch();
      void queryClient.invalidateQueries({ queryKey: ["complaints"] });
      toast.push("Complaint updated.");
    },
  });

  async function upload(file: File | null) {
    if (!file) return;
    const form = new FormData();
    form.append("file", file);
    setActionError(undefined);
    try {
      await apiRequest(`/complaints/${id}/attachments`, {
        method: "POST",
        body: form,
      });
      toast.push("Attachment uploaded.");
    } catch (error) {
      setActionError(errorMessage(error));
    }
  }

  if (query.isLoading) return <Skeleton className="h-[32rem]" />;
  if (query.error || !query.data) {
    return (
      <Alert tone="danger" title="Complaint unavailable">
        {errorMessage(query.error)}
      </Alert>
    );
  }

  const complaint = query.data;
  const status = enumCode("complaintStatus", complaint.status);
  const management = isSupervisorOrManager(identity);
  const writable = canMutateTenant(identity);
  const canAddMessage = writable && !["Closed", "Rejected"].includes(status);

  return (
    <>
      <PageHeader
        title={`${complaint.complaintNumber} · ${complaint.title}`}
        description="Visibility and message filtering are enforced by the API."
        actions={<Badge tone={statusTone(status)}>{status}</Badge>}
      />
      {actionError || actionMutation.error ? (
        <Alert tone="danger">
          {actionError ?? errorMessage(actionMutation.error)}
        </Alert>
      ) : null}
      <div className="mt-5 grid gap-5 xl:grid-cols-[1fr_22rem]">
        <div className="grid content-start gap-5">
          <DetailGrid
            data={complaint as unknown as Record<string, unknown>}
            omit={["messages", "status", "title"]}
          />
          <Card>
            <h2 className="text-lg font-black">Description</h2>
            <p className="mt-3 text-sm whitespace-pre-wrap">
              {complaint.description}
            </p>
          </Card>
          <Card>
            <h2 className="mb-4 text-lg font-black">Conversation</h2>
            {complaint.messages.length === 0 ? (
              <EmptyState
                title="No messages"
                description="Messages will appear here in chronological order."
              />
            ) : (
              <ol className="grid gap-3">
                {complaint.messages.map((entry) => (
                  <li
                    key={entry.id}
                    className="border-ink-950/10 rounded-xl border p-4"
                  >
                    <div className="mb-2 flex flex-wrap items-center gap-2">
                      <p className="text-xs font-bold">{entry.senderUserId}</p>
                      {entry.isInternal ? (
                        <Badge tone="warning">Internal</Badge>
                      ) : null}
                      <time className="text-ink-600 text-xs">
                        {formatDateTime(entry.createdAt, locale)}
                      </time>
                    </div>
                    <p className="text-sm whitespace-pre-wrap">
                      {entry.message}
                    </p>
                  </li>
                ))}
              </ol>
            )}
          </Card>
          <Card>
            <h2 className="mb-3 text-lg font-black">Add message</h2>
            <Field label="Message">
              <Textarea
                value={message}
                disabled={!canAddMessage}
                onChange={(event) => setMessage(event.target.value)}
              />
            </Field>
            {management ? (
              <label className="mt-3 flex items-center gap-2 text-sm font-semibold">
                <input
                  type="checkbox"
                  checked={internal}
                  disabled={!canAddMessage}
                  onChange={(event) => setInternal(event.target.checked)}
                />
                Internal management message
              </label>
            ) : null}
            <Button
              className="mt-3"
              disabled={!canAddMessage || !message.trim()}
              busy={actionMutation.isPending}
              onClick={() =>
                actionMutation.mutate({
                  action: "messages",
                  body: { message: message.trim(), isInternal: internal },
                })
              }
            >
              Send message
            </Button>
          </Card>
        </div>
        <div className="grid content-start gap-5">
          {management ? (
            <Card>
              <h2 className="mb-3 text-lg font-black">Management workflow</h2>
              <div className="grid gap-2">
                {status === "Submitted" ? (
                  <Button
                    disabled={!writable}
                    onClick={() =>
                      actionMutation.mutate({ action: "start-review" })
                    }
                  >
                    Start review
                  </Button>
                ) : null}
                {status === "UnderReview" ? (
                  <Button
                    disabled={!writable || !message.trim()}
                    onClick={() =>
                      actionMutation.mutate({
                        action: "request-information",
                        body: { message: message.trim(), isInternal: false },
                      })
                    }
                  >
                    Request information
                  </Button>
                ) : null}
                {status === "InProgress" ? (
                  <Button
                    disabled={!writable || !message.trim()}
                    onClick={() =>
                      actionMutation.mutate({
                        action: "respond",
                        body: { message: message.trim(), isInternal: false },
                      })
                    }
                  >
                    Respond and resolve
                  </Button>
                ) : null}
                {["UnderReview", "InProgress", "Resolved"].includes(status) ? (
                  <Button
                    variant="secondary"
                    disabled={!writable}
                    onClick={() => actionMutation.mutate({ action: "close" })}
                  >
                    Close complaint
                  </Button>
                ) : null}
              </div>
              <div className="border-ink-950/10 my-4 border-t" />
              <Field label="Assign reviewer">
                <Select
                  value={assignee}
                  disabled={!writable}
                  onChange={(event) => setAssignee(event.target.value)}
                >
                  <option value="">Select manager or supervisor</option>
                  {references.members
                    .filter((member) =>
                      ["Manager", "Supervisor"].includes(
                        enumCode("organizationRole", member.role),
                      ),
                    )
                    .map((member) => (
                      <option key={member.userId} value={member.userId}>
                        {member.fullName}
                      </option>
                    ))}
                </Select>
              </Field>
              <Button
                className="mt-2 w-full"
                variant="secondary"
                disabled={!writable || !assignee}
                onClick={() =>
                  actionMutation.mutate({
                    action: "assign",
                    body: { assigneeUserId: assignee },
                  })
                }
              >
                Assign
              </Button>
            </Card>
          ) : null}
          <Card>
            <FileUploader
              label="Complaint attachment"
              disabled={!writable}
              onChange={upload}
            />
            <p className="text-ink-600 mt-3 text-xs">
              The current complaint DTO does not return attachment metadata, so
              uploaded files cannot be listed after reload.
            </p>
          </Card>
        </div>
      </div>
    </>
  );
}
