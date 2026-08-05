"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { useLocale } from "next-intl";
import { useSearchParams } from "next/navigation";
import { z } from "zod";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  Field,
  FileUploader,
  Input,
  Select,
  Skeleton,
  Textarea,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode, enumCodes, enumValue, statusTone } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import { canMutateTenant } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";
import {
  formatDateTime,
  localInputToIso,
  toLocalInputValue,
} from "@/lib/utils";

import { DetailGrid } from "../shared/detail-grid";
import { useReferenceData } from "../shared/reference-data";

const templateItemSchema = z.object({
  name: z.string().trim().min(1).max(250),
  description: z.string().trim().max(1000),
  unitCode: z.string(),
  customUnitLabel: z.string().trim().max(100),
  defaultQuantity: z.string(),
  minimumQuantity: z.string(),
  imageUrl: z.string(),
  isActive: z.boolean(),
});
const orderTemplateSchema = z
  .object({
    branchId: z.string().uuid(),
    name: z.string().trim().min(2).max(250),
    description: z.string().trim().max(2000),
    sourceDepartmentId: z.string().uuid(),
    targetDepartmentId: z.string().uuid(),
    requiresApproval: z.boolean(),
    allowCustomItems: z.boolean(),
    isActive: z.boolean(),
    items: z.array(templateItemSchema).min(1),
  })
  .refine((values) => values.sourceDepartmentId !== values.targetDepartmentId, {
    path: ["targetDepartmentId"],
    message: "Source and target departments must differ.",
  });
type OrderTemplateValues = z.infer<typeof orderTemplateSchema>;

function emptyTemplateItem(): OrderTemplateValues["items"][number] {
  return {
    name: "",
    description: "",
    unitCode: "Each",
    customUnitLabel: "",
    defaultQuantity: "1",
    minimumQuantity: "0",
    imageUrl: "",
    isActive: true,
  };
}

export function OrderTemplateForm({ id }: { id?: string }) {
  const references = useReferenceData();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: id
      ? queryKeys.orderTemplates.detail(id)
      : ["order-template", "new"],
    queryFn: () =>
      apiRequest<Schemas["OrderTemplateDto"]>(`/order-templates/${id}`),
    enabled: Boolean(id),
  });
  const form = useForm<OrderTemplateValues>({
    resolver: zodResolver(orderTemplateSchema),
    defaultValues: {
      branchId: "",
      name: "",
      description: "",
      sourceDepartmentId: "",
      targetDepartmentId: "",
      requiresApproval: false,
      allowCustomItems: false,
      isActive: true,
      items: [emptyTemplateItem()],
    },
  });
  const items = useFieldArray({ control: form.control, name: "items" });
  const watchedItems = useWatch({ control: form.control, name: "items" });
  useEffect(() => {
    if (!query.data) return;
    form.reset({
      branchId: query.data.branchId,
      name: query.data.name,
      description: query.data.description ?? "",
      sourceDepartmentId: query.data.sourceDepartmentId,
      targetDepartmentId: query.data.targetDepartmentId,
      requiresApproval: query.data.requiresApproval,
      allowCustomItems: query.data.allowCustomItems,
      isActive: query.data.isActive,
      items: query.data.items.map((item) => ({
        name: item.name,
        description: item.description ?? "",
        unitCode: enumCode("unit", item.unitCode),
        customUnitLabel: item.customUnitLabel ?? "",
        defaultQuantity:
          item.defaultQuantity === null ? "" : String(item.defaultQuantity),
        minimumQuantity:
          item.minimumQuantity === null ? "" : String(item.minimumQuantity),
        imageUrl: item.imageUrl ?? "",
        isActive: item.isActive,
      })),
    });
  }, [form, query.data]);
  const mutation = useMutation({
    mutationFn: (values: OrderTemplateValues) =>
      apiRequest<Schemas["OrderTemplateDto"]>(
        id ? `/order-templates/${id}` : "/order-templates",
        {
          method: id ? "PATCH" : "POST",
          body: {
            branchId: values.branchId,
            name: values.name,
            description: values.description || null,
            sourceDepartmentId: values.sourceDepartmentId,
            targetDepartmentId: values.targetDepartmentId,
            requiresApproval: values.requiresApproval,
            allowCustomItems: values.allowCustomItems,
            isActive: values.isActive,
            items: values.items.map((item, index) => ({
              name: item.name,
              description: item.description || null,
              unitCode: enumValue("unit", item.unitCode),
              customUnitLabel:
                item.unitCode === "Custom" ? item.customUnitLabel : null,
              defaultQuantity: item.defaultQuantity
                ? Number(item.defaultQuantity)
                : null,
              minimumQuantity: item.minimumQuantity
                ? Number(item.minimumQuantity)
                : null,
              sortOrder: index,
              imageUrl: item.imageUrl || null,
              isActive: item.isActive,
            })),
          },
        },
      ),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: ["order-templates"] });
      toast.push(id ? "Order template updated." : "Order template created.");
      router.push(`/order-templates/${data.id}`);
    },
  });

  return (
    <>
      <PageHeader
        title={id ? "Edit order template" : "Create order template"}
        description="Items define order snapshots only; no inventory fields are used."
      />
      <form
        className="grid gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {mutation.error ? (
          <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field label="Name" required>
            <Input {...form.register("name")} />
          </Field>
          <Field label="Branch" required>
            <Select {...form.register("branchId")}>
              <option value="">Select branch</option>
              {references.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Source department" required>
            <Select {...form.register("sourceDepartmentId")}>
              <option value="">Select source</option>
              {references.departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            label="Target department"
            error={form.formState.errors.targetDepartmentId?.message}
            required
          >
            <Select {...form.register("targetDepartmentId")}>
              <option value="">Select target</option>
              {references.departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </Select>
          </Field>
          <div className="md:col-span-2">
            <Field label="Description">
              <Textarea {...form.register("description")} />
            </Field>
          </div>
          <div className="flex flex-wrap gap-6 md:col-span-2">
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" {...form.register("allowCustomItems")} />
              Allow custom items
            </label>
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" {...form.register("requiresApproval")} />
              Requires approval
            </label>
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" {...form.register("isActive")} />
              Active
            </label>
          </div>
        </Card>
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-black">Template items</h2>
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={() => items.append(emptyTemplateItem())}
            >
              <Plus className="size-4" /> Add item
            </Button>
          </div>
          <div className="grid gap-4">
            {items.fields.map((item, index) => {
              const unit = watchedItems[index]?.unitCode;
              return (
                <div
                  key={item.id}
                  className="border-ink-950/10 grid gap-3 rounded-xl border p-4 md:grid-cols-2"
                >
                  <Field label={`Item ${index + 1}`} required>
                    <Input {...form.register(`items.${index}.name`)} />
                  </Field>
                  <Field label="Unit" required>
                    <Select {...form.register(`items.${index}.unitCode`)}>
                      {enumCodes.unit.map((value) => (
                        <option key={value}>{value}</option>
                      ))}
                    </Select>
                  </Field>
                  {unit === "Custom" ? (
                    <Field label="Custom unit" required>
                      <Input
                        {...form.register(`items.${index}.customUnitLabel`)}
                      />
                    </Field>
                  ) : null}
                  <Field label="Default quantity">
                    <Input
                      type="number"
                      min="0"
                      step="0.01"
                      {...form.register(`items.${index}.defaultQuantity`)}
                    />
                  </Field>
                  <Field label="Minimum quantity">
                    <Input
                      type="number"
                      min="0"
                      step="0.01"
                      {...form.register(`items.${index}.minimumQuantity`)}
                    />
                  </Field>
                  <Field
                    label="Image URL"
                    hint="The backend accepts URL metadata; no image upload endpoint is exposed."
                  >
                    <Input
                      type="url"
                      {...form.register(`items.${index}.imageUrl`)}
                    />
                  </Field>
                  <div className="md:col-span-2">
                    <Field label="Description">
                      <Textarea
                        className="min-h-20"
                        {...form.register(`items.${index}.description`)}
                      />
                    </Field>
                  </div>
                  <div className="flex flex-wrap items-center justify-between gap-2 md:col-span-2">
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        {...form.register(`items.${index}.isActive`)}
                      />{" "}
                      Active item
                    </label>
                    <div className="flex gap-1">
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        disabled={index === 0}
                        onClick={() => items.move(index, index - 1)}
                        aria-label="Move item up"
                      >
                        <ArrowUp className="size-4" />
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        disabled={index === items.fields.length - 1}
                        onClick={() => items.move(index, index + 1)}
                        aria-label="Move item down"
                      >
                        <ArrowDown className="size-4" />
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        disabled={items.fields.length === 1}
                        onClick={() => items.remove(index)}
                        aria-label="Remove item"
                      >
                        <Trash2 className="text-danger-700 size-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
        <div>
          <Button type="submit" busy={mutation.isPending}>
            {id ? "Save template" : "Create template"}
          </Button>
        </div>
      </form>
    </>
  );
}

const orderItemSchema = z.object({
  templateItemId: z.string(),
  name: z.string().trim().min(1),
  description: z.string(),
  unitCode: z.string(),
  customUnitLabel: z.string(),
  requestedQuantity: z.number().positive(),
  note: z.string(),
});
const orderSchema = z
  .object({
    branchId: z.string(),
    sourceDepartmentId: z.string(),
    targetDepartmentId: z.string(),
    requiredAt: z.string(),
    priority: z.string(),
    generalNote: z.string(),
    items: z.array(orderItemSchema).min(1),
  })
  .refine((values) => values.sourceDepartmentId !== values.targetDepartmentId, {
    path: ["targetDepartmentId"],
    message: "Source and target must differ.",
  });
type OrderValues = z.infer<typeof orderSchema>;

export function OrderForm() {
  const searchParams = useSearchParams();
  const templateId = searchParams.get("templateId");
  const references = useReferenceData();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [defaultRequiredAt] = useState(() =>
    toLocalInputValue(new Date(Date.now() + 2 * 60 * 60_000).toISOString()),
  );
  const template = useQuery({
    queryKey: templateId
      ? queryKeys.orderTemplates.detail(templateId)
      : ["order-template", "none"],
    queryFn: () =>
      apiRequest<Schemas["OrderTemplateDto"]>(`/order-templates/${templateId}`),
    enabled: Boolean(templateId),
  });
  const form = useForm<OrderValues>({
    resolver: zodResolver(orderSchema),
    defaultValues: {
      branchId: "",
      sourceDepartmentId: "",
      targetDepartmentId: "",
      requiredAt: defaultRequiredAt,
      priority: "Normal",
      generalNote: "",
      items: [
        {
          templateItemId: "",
          name: "",
          description: "",
          unitCode: "Each",
          customUnitLabel: "",
          requestedQuantity: 1,
          note: "",
        },
      ],
    },
  });
  const items = useFieldArray({ control: form.control, name: "items" });
  const watchedItems = useWatch({ control: form.control, name: "items" });
  useEffect(() => {
    if (!template.data) return;
    form.reset({
      branchId: template.data.branchId,
      sourceDepartmentId: template.data.sourceDepartmentId,
      targetDepartmentId: template.data.targetDepartmentId,
      requiredAt: toLocalInputValue(
        new Date(Date.now() + 2 * 60 * 60_000).toISOString(),
      ),
      priority: "Normal",
      generalNote: "",
      items: template.data.items
        .filter((item) => item.isActive)
        .map((item) => ({
          templateItemId: item.id,
          name: item.name,
          description: item.description ?? "",
          unitCode: enumCode("unit", item.unitCode),
          customUnitLabel: item.customUnitLabel ?? "",
          requestedQuantity: Number(item.defaultQuantity ?? 1),
          note: "",
        })),
    });
  }, [form, template.data]);
  const allowCustom = template.data?.allowCustomItems ?? true;
  const mutation = useMutation({
    mutationFn: (values: OrderValues) => {
      const payloadItems = values.items.map((item) => ({
        templateItemId: item.templateItemId || null,
        customName: item.templateItemId ? null : item.name,
        description: item.description || null,
        unitCode: item.templateItemId ? null : enumValue("unit", item.unitCode),
        customUnitLabel:
          !item.templateItemId && item.unitCode === "Custom"
            ? item.customUnitLabel
            : null,
        requestedQuantity: item.requestedQuantity,
        note: item.note || null,
      }));
      const common = {
        requiredAt: values.requiredAt
          ? localInputToIso(values.requiredAt)
          : null,
        priority: enumValue("priority", values.priority),
        generalNote: values.generalNote || null,
        items: payloadItems,
      };
      return templateId
        ? apiRequest<Schemas["DepartmentOrderDto"]>(
            `/order-templates/${templateId}/create-order`,
            { method: "POST", body: common },
          )
        : apiRequest<Schemas["DepartmentOrderDto"]>("/department-orders", {
            method: "POST",
            body: {
              ...common,
              orderTemplateId: null,
              branchId: values.branchId,
              sourceDepartmentId: values.sourceDepartmentId,
              targetDepartmentId: values.targetDepartmentId,
            },
          });
    },
    onSuccess: (order) => {
      void queryClient.invalidateQueries({ queryKey: ["department-orders"] });
      toast.push("Department order created.");
      router.push(`/department-orders/${order.id}`);
    },
  });
  return (
    <>
      <PageHeader
        title={
          templateId ? "Create order from template" : "Create department order"
        }
        description="This request records quantities and snapshots only; it does not reserve stock."
      />
      <form
        className="grid gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {mutation.error ? (
          <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field label="Branch" required>
            <Select
              disabled={Boolean(templateId)}
              {...form.register("branchId")}
            >
              <option value="">Select branch</option>
              {references.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Priority">
            <Select {...form.register("priority")}>
              {enumCodes.priority.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </Select>
          </Field>
          <Field label="Source department" required>
            <Select
              disabled={Boolean(templateId)}
              {...form.register("sourceDepartmentId")}
            >
              <option value="">Select source</option>
              {references.departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            label="Target department"
            error={form.formState.errors.targetDepartmentId?.message}
            required
          >
            <Select
              disabled={Boolean(templateId)}
              {...form.register("targetDepartmentId")}
            >
              <option value="">Select target</option>
              {references.departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Required at">
            <Input type="datetime-local" {...form.register("requiredAt")} />
          </Field>
          <div className="md:col-span-2">
            <Field label="General note">
              <Textarea {...form.register("generalNote")} />
            </Field>
          </div>
        </Card>
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-black">Requested items</h2>
            {allowCustom ? (
              <Button
                type="button"
                size="sm"
                variant="secondary"
                onClick={() =>
                  items.append({
                    templateItemId: "",
                    name: "",
                    description: "",
                    unitCode: "Each",
                    customUnitLabel: "",
                    requestedQuantity: 1,
                    note: "",
                  })
                }
              >
                <Plus className="size-4" /> Custom item
              </Button>
            ) : null}
          </div>
          <div className="grid gap-3">
            {items.fields.map((item, index) => (
              <div
                key={item.id}
                className="border-ink-950/10 grid gap-3 rounded-xl border p-4 md:grid-cols-[1fr_10rem_auto]"
              >
                <div>
                  <Field label={`Item ${index + 1}`} required>
                    <Input
                      readOnly={Boolean(watchedItems[index]?.templateItemId)}
                      {...form.register(`items.${index}.name`)}
                    />
                  </Field>
                  <Input
                    className="mt-2"
                    placeholder="Per-item note"
                    {...form.register(`items.${index}.note`)}
                  />
                </div>
                <Field label="Quantity" required>
                  <Input
                    type="number"
                    min="0.01"
                    step="0.01"
                    {...form.register(`items.${index}.requestedQuantity`, {
                      valueAsNumber: true,
                    })}
                  />
                </Field>
                <Button
                  type="button"
                  variant="ghost"
                  disabled={items.fields.length === 1}
                  onClick={() => items.remove(index)}
                  aria-label="Remove order item"
                >
                  <Trash2 className="text-danger-700 size-4" />
                </Button>
              </div>
            ))}
          </div>
        </Card>
        <div>
          <Button type="submit" busy={mutation.isPending}>
            Send order
          </Button>
        </div>
      </form>
    </>
  );
}

export function OrderDetail({ id }: { id: string }) {
  const locale = useLocale();
  const { identity } = useAuth();
  const references = useReferenceData();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const [assignee, setAssignee] = useState("");
  const [actionError, setActionError] = useState<string>();
  const query = useQuery({
    queryKey: queryKeys.orders.detail(id),
    queryFn: () =>
      apiRequest<Schemas["DepartmentOrderDto"]>(`/department-orders/${id}`),
  });
  const mutation = useMutation({
    mutationFn: ({
      action,
      body,
    }: {
      action: string;
      body?: Record<string, unknown>;
    }) =>
      apiRequest<Schemas["DepartmentOrderDto"]>(
        `/department-orders/${id}/${action}`,
        { method: "POST", body },
      ),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.orders.detail(id), data);
      void queryClient.invalidateQueries({ queryKey: ["department-orders"] });
      toast.push("Order updated.");
    },
  });
  async function updateItem(
    item: Schemas["DepartmentOrderItemDto"],
    values: { fulfilled: number; received: number; status: string },
  ) {
    setActionError(undefined);
    try {
      await apiRequest(`/department-orders/${id}/items/${item.id}`, {
        method: "PATCH",
        body: {
          fulfilledQuantity: values.fulfilled,
          receivedQuantity: values.received,
          status: enumValue("orderItemStatus", values.status),
          fulfillmentNote: item.fulfillmentNote,
        },
      });
      await query.refetch();
      toast.push("Order item updated.");
    } catch (error) {
      setActionError(errorMessage(error));
    }
  }
  async function upload(file: File | null) {
    if (!file) return;
    const form = new FormData();
    form.append("file", file);
    try {
      await apiRequest(`/department-orders/${id}/attachments`, {
        method: "POST",
        body: form,
      });
      toast.push("Attachment uploaded.");
    } catch (error) {
      setActionError(errorMessage(error));
    }
  }
  if (query.isLoading) return <Skeleton className="h-[34rem]" />;
  if (query.error || !query.data)
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  const order = query.data;
  const status = enumCode("orderStatus", order.status);
  const writable = canMutateTenant(identity);
  return (
    <>
      <PageHeader
        title={`Order ${order.orderNumber}`}
        description={`${formatDateTime(order.requestedAt, locale)} · required ${formatDateTime(order.requiredAt, locale)}`}
        actions={
          <>
            <Badge tone={statusTone(status)}>{status}</Badge>
            {order.isLate ? <Badge tone="danger">Late</Badge> : null}
          </>
        }
      />
      {actionError || mutation.error ? (
        <Alert tone="danger">
          {actionError ?? errorMessage(mutation.error)}
        </Alert>
      ) : null}
      <div className="mt-5 grid gap-5 xl:grid-cols-[1fr_21rem]">
        <div className="grid gap-5">
          <DetailGrid
            data={order as unknown as Record<string, unknown>}
            omit={["items", "status"]}
          />
          <Card>
            <h2 className="mb-4 text-lg font-black">Order items</h2>
            <div className="grid gap-3">
              {order.items.map((item) => (
                <OrderItemEditor
                  key={item.id}
                  item={item}
                  disabled={!writable}
                  onSave={(values) => void updateItem(item, values)}
                />
              ))}
            </div>
          </Card>
        </div>
        <div className="grid content-start gap-5">
          <Card>
            <h2 className="mb-3 text-lg font-black">Workflow actions</h2>
            <div className="grid gap-2">
              <Button
                variant="secondary"
                disabled={!writable}
                onClick={() => mutation.mutate({ action: "view" })}
              >
                Acknowledge view
              </Button>
              {status === "Submitted" ? (
                <Button
                  disabled={!writable}
                  onClick={() => mutation.mutate({ action: "accept" })}
                >
                  Accept
                </Button>
              ) : null}
              {status === "Accepted" ? (
                <Button
                  disabled={!writable}
                  onClick={() => mutation.mutate({ action: "start" })}
                >
                  Start preparation
                </Button>
              ) : null}
              {status === "Preparing" ? (
                <Button
                  disabled={!writable}
                  onClick={() => mutation.mutate({ action: "mark-ready" })}
                >
                  Mark ready
                </Button>
              ) : null}
              {status === "Ready" ? (
                <Button
                  disabled={!writable}
                  onClick={() => mutation.mutate({ action: "deliver" })}
                >
                  Confirm delivery
                </Button>
              ) : null}
              {status === "Delivered" ? (
                <Button
                  disabled={!writable}
                  onClick={() => mutation.mutate({ action: "confirm-receipt" })}
                >
                  Confirm receipt
                </Button>
              ) : null}
            </div>
          </Card>
          <Card>
            <Field label="Assign to">
              <Select
                value={assignee}
                onChange={(event) => setAssignee(event.target.value)}
              >
                <option value="">Select member</option>
                {references.members.map((member) => (
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
                mutation.mutate({
                  action: "assign",
                  body: { assigneeUserId: assignee },
                })
              }
            >
              Assign
            </Button>
            <div className="border-ink-950/10 my-4 border-t" />
            <Field label="Reason">
              <Textarea
                className="min-h-20"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </Field>
            <div className="mt-2 grid gap-2">
              <Button
                variant="secondary"
                disabled={!writable || !reason.trim()}
                onClick={() =>
                  mutation.mutate({
                    action: "reject",
                    body: { reason: reason.trim() },
                  })
                }
              >
                Reject
              </Button>
              <Button
                variant="danger"
                disabled={!writable || !reason.trim()}
                onClick={() =>
                  mutation.mutate({
                    action: "cancel",
                    body: { reason: reason.trim() },
                  })
                }
              >
                Cancel order
              </Button>
            </div>
          </Card>
          <Card>
            <FileUploader label="Order attachment" onChange={upload} />
            <p className="text-ink-600 mt-3 text-xs">
              Attachment collections are not returned by the current order DTO,
              so uploaded files cannot be listed after reload.
            </p>
          </Card>
        </div>
      </div>
    </>
  );
}

function OrderItemEditor({
  item,
  disabled,
  onSave,
}: {
  item: Schemas["DepartmentOrderItemDto"];
  disabled: boolean;
  onSave: (values: {
    fulfilled: number;
    received: number;
    status: string;
  }) => void;
}) {
  const [fulfilled, setFulfilled] = useState(Number(item.fulfilledQuantity));
  const [received, setReceived] = useState(Number(item.receivedQuantity));
  const [status, setStatus] = useState(
    enumCode("orderItemStatus", item.status),
  );
  return (
    <div className="border-ink-950/10 grid gap-3 rounded-xl border p-4 md:grid-cols-[1fr_8rem_8rem_12rem_auto] md:items-end">
      <div>
        <p className="font-bold">{item.name}</p>
        <p className="text-ink-600 text-xs">
          Requested {String(item.requestedQuantity)}{" "}
          {item.customUnitLabel ?? enumCode("unit", item.unitCode)}
        </p>
      </div>
      <Field label="Fulfilled">
        <Input
          type="number"
          min="0"
          step="0.01"
          value={fulfilled}
          disabled={disabled}
          onChange={(event) => setFulfilled(Number(event.target.value))}
        />
      </Field>
      <Field label="Received">
        <Input
          type="number"
          min="0"
          step="0.01"
          value={received}
          disabled={disabled}
          onChange={(event) => setReceived(Number(event.target.value))}
        />
      </Field>
      <Field label="Item status">
        <Select
          value={status}
          disabled={disabled}
          onChange={(event) => setStatus(event.target.value)}
        >
          {enumCodes.orderItemStatus.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </Select>
      </Field>
      <Button
        size="sm"
        variant="secondary"
        disabled={disabled}
        onClick={() => onSave({ fulfilled, received, status })}
      >
        Save
      </Button>
    </div>
  );
}

export function OrderTemplateDetail({ id }: { id: string }) {
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: queryKeys.orderTemplates.detail(id),
    queryFn: () =>
      apiRequest<Schemas["OrderTemplateDto"]>(`/order-templates/${id}`),
  });
  const mutation = useMutation({
    mutationFn: (action: "clone" | "activate" | "deactivate" | "delete") =>
      apiRequest(
        action === "delete"
          ? `/order-templates/${id}`
          : `/order-templates/${id}/${action}`,
        { method: action === "delete" ? "DELETE" : "POST" },
      ),
    onSuccess: (_, action) => {
      void queryClient.invalidateQueries({ queryKey: ["order-templates"] });
      toast.push(`Template ${action} completed.`);
      if (action === "delete") router.push("/order-templates");
      else void query.refetch();
    },
  });
  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error || !query.data)
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  return (
    <>
      <PageHeader
        title={query.data.name}
        description="Order instances preserve these item snapshots."
        actions={
          <>
            <Button variant="secondary">
              <Link href={`/order-templates/${id}/edit`}>Edit</Link>
            </Button>
            <Button>
              <Link href={`/department-orders/new?templateId=${id}`}>
                Create order
              </Link>
            </Button>
            <Button
              variant="secondary"
              onClick={() => mutation.mutate("clone")}
            >
              Clone
            </Button>
          </>
        }
      />
      <DetailGrid
        data={query.data as unknown as Record<string, unknown>}
        omit={["items", "name"]}
      />
      <Card className="mt-5">
        <h2 className="mb-4 text-lg font-black">Selectable items</h2>
        <div className="grid gap-3">
          {query.data.items.map((item) => (
            <div
              key={item.id}
              className="border-ink-950/10 flex items-start justify-between gap-4 rounded-xl border p-4"
            >
              <div>
                <p className="font-bold">{item.name}</p>
                <p className="text-ink-600 text-sm">{item.description}</p>
              </div>
              <Badge>
                {String(item.defaultQuantity ?? "—")}{" "}
                {item.customUnitLabel ?? enumCode("unit", item.unitCode)}
              </Badge>
            </div>
          ))}
        </div>
      </Card>
      <div className="mt-5 flex gap-2">
        <Button
          variant="secondary"
          onClick={() =>
            mutation.mutate(query.data.isActive ? "deactivate" : "activate")
          }
        >
          {query.data.isActive ? "Deactivate" : "Activate"}
        </Button>
        <Button variant="danger" onClick={() => mutation.mutate("delete")}>
          Delete
        </Button>
      </div>
    </>
  );
}
