"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowUp,
  Plus,
  Trash2,
  HelpCircle,
  Sliders,
  Type,
  AlignLeft,
  ListOrdered,
  Info,
  X,
  Folder,
  FolderTree,
} from "lucide-react";
import { useFieldArray, useForm } from "react-hook-form";
import { z } from "zod";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
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
import { useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCodes, enumValue } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

import { useReferenceData } from "../shared/reference-data";
import { TreeChecklistBuilder } from "./tree-checklist-builder";

const itemSchema = z.object({
  id: z.string().optional(),
  title: z.string().trim().min(1).max(250),
  description: z.string().trim().max(1000),
  isRequired: z.boolean(),
  evidenceMode: z.string(),
  itemType: z.string().optional(),
  options: z.string().optional(),
  mainBlockTitle: z.string().optional(),
  subBlockTitle: z.string().optional(),
});
const templateSchema = z.object({
  defaultDepartmentId: z.string(),
  title: z.string().trim().min(2).max(250),
  description: z.string().trim().max(2000),
  defaultPriority: z.string(),
  defaultDurationMinutes: z.string(),
  requiresApproval: z.boolean(),
  isActive: z.boolean(),
  items: z.array(itemSchema).min(1),
});
type TemplateValues = z.infer<typeof templateSchema>;

function emptyItem(itemType = "SingleLineText"): TemplateValues["items"][number] {
  return {
    title: "",
    description: "",
    isRequired: true,
    evidenceMode: "None",
    itemType,
    options: itemType === "MultipleChoice" ? "Option 1, Option 2, Option 3" : "",
    mainBlockTitle: "",
    subBlockTitle: "",
  };
}

export function TaskTemplateForm({ id }: { id?: string }) {
  const references = useReferenceData();
  const queryClient = useQueryClient();
  const toast = useToast();
  const router = useRouter();
  const [uploadError, setUploadError] = useState<string>();
  const [isTypeModalOpen, setIsTypeModalOpen] = useState(false);
  const query = useQuery({
    queryKey: id
      ? queryKeys.taskTemplates.detail(id)
      : ["task-template", "new"],
    queryFn: () =>
      apiRequest<Schemas["TaskTemplateDto"]>(`/task-templates/${id}`),
    enabled: Boolean(id),
  });
  const form = useForm<TemplateValues>({
    resolver: zodResolver(templateSchema) as any,
    defaultValues: {
      defaultDepartmentId: "",
      title: "",
      description: "",
      defaultPriority: "Normal",
      defaultDurationMinutes: "",
      requiresApproval: false,
      isActive: true,
      items: [emptyItem()],
    },
  });
 
  const items = useFieldArray({
    control: form.control,
    name: "items",
    keyName: "_key",
  });
  

  useEffect(() => {
    if (!query.data) return;
    form.reset({
      defaultDepartmentId: query.data.defaultDepartmentId ?? "",
      title: query.data.title,
      description: query.data.description ?? "",
      defaultPriority:
        enumCodes.priority[Number(query.data.defaultPriority)] ?? "Normal",
      defaultDurationMinutes:
        query.data.defaultDurationMinutes === null
          ? ""
          : String(query.data.defaultDurationMinutes),
      requiresApproval: query.data.requiresApproval,
      isActive: query.data.isActive,
      items: query.data.items.map((item) => ({
        id: item.id,
        title: item.title,
        description: item.description ?? "",
        isRequired: item.isRequired,
        evidenceMode:
          enumCodes.evidenceMode[Number(item.evidenceMode)] ?? "None",
        itemType: item.itemType
          ? enumCodes.taskItemType[Number(item.itemType)] ?? "Question"
          : "Question",
        options: item.options ?? "",
        mainBlockTitle: item.mainBlockTitle ?? "",
        subBlockTitle: item.subBlockTitle ?? "",
      })),
    });
  }, [form, query.data]);

  const mutation = useMutation({
    mutationFn: (values: TemplateValues) =>
      apiRequest<Schemas["TaskTemplateDto"]>(
        id ? `/task-templates/${id}` : "/task-templates",
        {
          method: id ? "PATCH" : "POST",
          body: {
            defaultDepartmentId: values.defaultDepartmentId || null,
            title: values.title,
            description: values.description || null,
            defaultPriority: enumValue("priority", values.defaultPriority),
            defaultDurationMinutes: values.defaultDurationMinutes
              ? Number(values.defaultDurationMinutes)
              : null,
            requiresApproval: values.requiresApproval,
            isActive: values.isActive,
            items: values.items.map((item, index) => ({
              title: item.title,
              description: item.description || null,
              sortOrder: index,
              isRequired: item.isRequired,
              evidenceMode: enumValue("evidenceMode", item.evidenceMode),
              itemType: enumValue("taskItemType", item.itemType || "SingleLineText"),
              options: item.options || null,
              mainBlockTitle: item.mainBlockTitle || null,
              subBlockTitle: item.subBlockTitle || null,
            })),
          },
        },
      ),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: ["task-templates"] });
      toast.push(id ? "Task template updated." : "Task template created.");
      router.push(`/task-templates/${data.id}`);
    },
  });

  async function uploadInstruction(itemId: string, file: File | null) {
    if (!file) return;
    const data = new FormData();
    data.append("file", file);
    setUploadError(undefined);
    try {
      await apiRequest(`/task-templates/${id}/items/${itemId}/instruction`, {
        method: "POST",
        body: data,
      });
      await query.refetch();
      toast.push("Instruction uploaded.");
    } catch (error) {
      setUploadError(errorMessage(error));
    }
  }

  function handleSelectItemType(type: string) {
    items.append(emptyItem(type));
    setIsTypeModalOpen(false);
  }

  if (query.isLoading) return <Skeleton className="h-[32rem]" />;
  if (query.error)
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;

  return (
    <>
      <PageHeader
        title={id ? `Edit ${query.data?.title ?? "template"}` : "Create task template"}
        description="Configure standard operational checklist rules and default parameters."
      />
      <form
        className="mt-5 grid gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {mutation.error || uploadError ? (
          <Alert tone="danger">
            {uploadError ?? errorMessage(mutation.error)}
          </Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field
            label="Title"
            error={form.formState.errors.title?.message}
            required
          >
            <Input {...form.register("title")} />
          </Field>
          <Field label="Default priority" required>
            <Select {...form.register("defaultPriority")}>
              {enumCodes.priority.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </Select>
          </Field>
          <Field
            label="Default department"
            error={form.formState.errors.defaultDepartmentId?.message}
          >
            <Select {...form.register("defaultDepartmentId")}>
              <option value="">No default department</option>
              {references.departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Default duration (minutes)">
            <Input
              type="number"
              min="1"
              {...form.register("defaultDurationMinutes")}
            />
          </Field>
          <div className="md:col-span-2">
            <Field label="Description">
              <Textarea {...form.register("description")} />
            </Field>
          </div>
          <div className="flex flex-wrap gap-6 md:col-span-2">
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

        <TreeChecklistBuilder
          items={form.watch("items")}
          onAppend={(newItem) => items.append(newItem)}
          onRemove={(idx) => items.remove(idx)}
          onMove={(from, to) => items.move(from, to)}
          onUpdate={(idx, updatedItem) =>
            form.setValue(`items.${idx}`, updatedItem, { shouldValidate: true })
          }
        />
        <div>
          <Button type="submit" busy={mutation.isPending}>
            {id ? "Save template" : "Create template"}
          </Button>
        </div>
      </form>
    </>
  );
}
