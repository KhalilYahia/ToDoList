"use client";

import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Download,
  Paperclip,
  Plus,
  Trash2,
  Folder,
  FolderTree,
  HelpCircle,
  Sliders,
  Type,
  AlignLeft,
  ListOrdered,
  Info,
  X,
  ArrowUp,
  ArrowDown,
  Check,
  FileCheck,
  Clock,
  AlertTriangle,
} from "lucide-react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { useLocale, useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { z } from "zod";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  FileUploader,
  Input,
  Select,
  Skeleton,
  Textarea,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode, enumCodes, enumValue, statusTone } from "@/lib/api/enums";
import { ApiError, errorMessage } from "@/lib/api/errors";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { canMutateTenant, isManager, tenantRole } from "@/lib/permissions/permissions";
import { useAuth } from "@/lib/auth/auth-provider";
import { queryKeys } from "@/lib/query/query-keys";
import {
  compactParams,
  formatDateTime,
  localInputToIso,
  toLocalInputValue,
} from "@/lib/utils";

import { DetailGrid } from "../shared/detail-grid";
import { useReferenceData } from "../shared/reference-data";
import {
  type AssignmentMode,
  TaskAssignmentFields,
} from "./task-assignment-fields";
import { TreeChecklistBuilder } from "./tree-checklist-builder";

const taskItemSchema = z.object({
  title: z.string().trim().min(1).max(250),
  description: z.string().trim().max(1000),
  isRequired: z.boolean(),
  evidenceMode: z.string(),
  itemType: z.string().optional(),
  options: z.string().optional(),
  mainBlockTitle: z.string().optional(),
  subBlockTitle: z.string().optional(),
});
const taskSchema = z
  .object({
    branchId: z.string(),
    departmentId: z.string().uuid(),
    assignmentMode: z.enum([
      "SingleUser",
      "SelectedUsers",
      "AllDepartmentMembers",
    ]),
    assigneeUserIds: z.array(z.string().uuid()),
    title: z.string().trim().min(2).max(250),
    description: z.string().trim().max(2000),
    scheduledStartAt: z.string().min(1),
    dueAt: z.string().min(1),
    priority: z.string(),
    requiresApproval: z.boolean(),
    items: z.array(taskItemSchema).min(1),
  })
  .refine(
    (values) => new Date(values.dueAt) > new Date(values.scheduledStartAt),
    { path: ["dueAt"], message: "Due time must be after start time." },
  )
  .superRefine((values, context) => {
    const expected =
      values.assignmentMode === "SingleUser"
        ? values.assigneeUserIds.length === 1
        : values.assignmentMode === "SelectedUsers"
          ? values.assigneeUserIds.length >= 2
          : values.assigneeUserIds.length === 0;
    if (!expected) {
      context.addIssue({
        code: "custom",
        path: ["assigneeUserIds"],
        message: "Choose the employees required by the assignment mode.",
      });
    }
  });
type TaskValues = z.infer<typeof taskSchema>;

function emptyTaskItem(itemType = "SingleLineText"): TaskValues["items"][number] {
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

function defaultStart() {
  const start = new Date();
  start.setMinutes(Math.ceil(start.getMinutes() / 15) * 15, 0, 0);
  return start;
}

export function TaskForm() {
  const references = useReferenceData();
  const searchParams = useSearchParams();
  const templateId = searchParams.get("templateId");
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();

  const [creationSource, setCreationSource] = useState<"scratch" | "template">(
    templateId ? "template" : "scratch",
  );
  const [selectedTemplateId, setSelectedTemplateId] = useState<string>(templateId || "");

  const template = useQuery({
    queryKey: templateId
      ? queryKeys.taskTemplates.detail(templateId)
      : ["task-template", "none"],
    queryFn: () =>
      apiRequest<Schemas["TaskTemplateDto"]>(`/task-templates/${templateId}`),
    enabled: Boolean(templateId),
  });

  const handleApplyTemplate = (targetId: string) => {
    if (!targetId) {
      toast.push("Пожалуйста, выберите шаблон из списка.");
      return;
    }
    const found = references.taskTemplates.find((t) => t.id === targetId);
    if (found && !found.isActive) {
      toast.push("Данный шаблон не активен. Пожалуйста, сначала активируйте его.");
      return;
    }
    router.push(`/tasks/new?templateId=${targetId}`);
  };

  const handleSourceChange = (mode: "scratch" | "template") => {
    setCreationSource(mode);
    if (mode === "scratch" && templateId) {
      router.push("/tasks/new");
    }
  };

  const start = useMemo(() => defaultStart(), []);
  const due = useMemo(() => new Date(start.getTime() + 60 * 60_000), [start]);
  const form = useForm<TaskValues>({
    resolver: zodResolver(taskSchema) as any,
    defaultValues: {
      branchId: "",
      departmentId: "",
      assignmentMode: "SingleUser",
      assigneeUserIds: [],
      title: "",
      description: "",
      scheduledStartAt: toLocalInputValue(start.toISOString()),
      dueAt: toLocalInputValue(due.toISOString()),
      priority: "Normal",
      requiresApproval: false,
      items: [emptyTaskItem()],
    },
  });

  const items = useFieldArray({ control: form.control, name: "items" });
  const [assignmentDepartmentId, assignmentMode, assignmentUserIds] = useWatch({
    control: form.control,
    name: ["departmentId", "assignmentMode", "assigneeUserIds"],
  });

  useEffect(() => {
    if (!template.data){ console.log("template.data", template.data);  return;}
    const duration = Number(template.data.defaultDurationMinutes ?? 60);
    form.reset({
      branchId: "",
      departmentId: template.data.defaultDepartmentId ?? "",
      assignmentMode: "SingleUser",
      assigneeUserIds: [],
      title: template.data.title,
      description: template.data.description ?? "",
      scheduledStartAt: toLocalInputValue(start.toISOString()),
      dueAt: toLocalInputValue(
        new Date(start.getTime() + duration * 60_000).toISOString(),
      ),
      priority: enumCode("priority", template.data.defaultPriority),
      requiresApproval: template.data.requiresApproval,
      items: template.data.items.map((item) => ({
        title: item.title,
        description: item.description ?? "",
        isRequired: item.isRequired,
        evidenceMode: enumCode("evidenceMode", item.evidenceMode),
        itemType: item.itemType
          ? enumCodes.taskItemType[Number(item.itemType)] ?? "Question"
          : "Question",
        options: item.options ?? "",
        mainBlockTitle: item.mainBlockTitle ?? "",
        subBlockTitle: item.subBlockTitle ?? "",
      })),
    });
  }, [form, start, template.data]);

  const mutation = useMutation({
    mutationFn: (values: TaskValues) => {
      console.log("mutationFn", values);
      if (template.data && !template.data.isActive) {
        throw new Error("Данный шаблон не активен. Пожалуйста, сначала активируйте его.");
      }
      const common = {
        departmentId: values.departmentId,
        assignment: {
          mode: enumValue("taskAssignmentMode", values.assignmentMode),
          userIds: values.assigneeUserIds,
        },
        scheduledStartAt: localInputToIso(values.scheduledStartAt),
        dueAt: localInputToIso(values.dueAt),
        priority: enumValue("priority", values.priority),
        requiresApproval: values.requiresApproval,
        items: values.items.map((item, index) => ({
          title: item.title,
          description: item.description || null,
          sortOrder: index,
          isRequired: item.isRequired,
          evidenceMode: enumValue("evidenceMode", item.evidenceMode),
          itemType: enumValue("taskItemType", item.itemType || "Question"),
          options: item.options || null,
          mainBlockTitle: item.mainBlockTitle || null,
          subBlockTitle: item.subBlockTitle || null,
        })),
      };
      return templateId
        ? apiRequest<Schemas["TaskDistributionResponse"]>(
            `/task-templates/${templateId}/create-task`,
            { method: "POST", body: common },
          )
        : apiRequest<Schemas["TaskDistributionResponse"]>("/tasks", {
            method: "POST",
            body: {
              ...common,
              branchId: values.branchId,
              title: values.title,
              description: values.description || null,
            },
          });
    },
    onSuccess: (distribution) => {
      void queryClient.invalidateQueries({ queryKey: ["tasks"] });
      toast.push("Task created.");
      const firstTask = distribution.tasks[0];
      if (firstTask) router.push(`/tasks/${firstTask.taskId}`);
    },
  });

  return (
    <>
      <PageHeader
        title={templateId ? "Создание задачи из шаблона" : "Создание новой задачи"}
        description={
          templateId
            ? "Проверьте данные задачи, заполненные из шаблона."
            : "Создайте задачу с нуля или выберите готовый шаблон."
        }
      />

      {/* CREATION MODE SELECTION & TEMPLATE SELECTOR */}
      <Card className="mb-5 grid gap-4 bg-surface-50 border border-ink-950/10">
        <h3 className="font-bold text-sm text-ink-900 uppercase tracking-wider">
          Источник создания задачи
        </h3>
        <div className="flex flex-wrap gap-4">
          <label className="flex items-center gap-2 cursor-pointer font-bold text-sm text-ink-800">
            <input
              type="radio"
              name="creationSource"
              checked={creationSource === "scratch"}
              onChange={() => handleSourceChange("scratch")}
            />
            Создать задачу с нуля (One-off Task)
          </label>
          <label className="flex items-center gap-2 cursor-pointer font-bold text-sm text-ink-800">
            <input
              type="radio"
              name="creationSource"
              checked={creationSource === "template"}
              onChange={() => handleSourceChange("template")}
            />
            Использовать существующий шаблон
          </label>
        </div>

        {creationSource === "template" ? (
          <div className="mt-2 flex flex-wrap items-center gap-3 pt-3 border-t border-ink-950/10">
            <div className="min-w-64 flex-1">
              <Select
                value={selectedTemplateId}
                onChange={(e) => setSelectedTemplateId(e.target.value)}
              >
                <option value="">Выберите шаблон из списка...</option>
                {references.taskTemplates.map((tpl) => (
                  <option key={tpl.id} value={tpl.id}>
                    {tpl.title} {!tpl.isActive ? " (НЕ АКТИВЕН)" : ""}
                  </option>
                ))}
              </Select>
            </div>
            <Button
              type="button"
              variant="secondary"
              onClick={() => handleApplyTemplate(selectedTemplateId)}
            >
              Использовать шаблон (Use Template)
            </Button>
          </div>
        ) : null}
      </Card>

      {template.data && !template.data.isActive ? (
        <div className="mb-5">
          <Alert tone="danger">
            <strong>Внимание:</strong> Выбранный шаблон отключен (не активен). Пожалуйста, сначала активируйте его перед созданием задачи.
          </Alert>
        </div>
      ) : null}
      {template.isLoading ? (
        <Skeleton className="h-96" />
      ) : (
        <form
          className="grid gap-5"
          onSubmit={form.handleSubmit((values) => {
            console.log("form values", values);
            mutation.mutate(values)
          })}
        >
          {mutation.error ? (
            <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
          ) : null}
          <Card className="grid gap-4 md:grid-cols-2">
            {!templateId ? (
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
            ) : null}
            <Field label="Department" required>
              <Select {...form.register("departmentId")}>
                <option value="">Select department</option>
                {references.departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {department.name}
                  </option>
                ))}
              </Select>
            </Field>
            <TaskAssignmentFields
              departmentId={assignmentDepartmentId}
              mode={assignmentMode as AssignmentMode}
              userIds={assignmentUserIds}
              members={references.members}
              onModeChange={(mode) =>
                form.setValue("assignmentMode", mode, {
                  shouldValidate: true,
                })
              }
              onUserIdsChange={(userIds) =>
                form.setValue("assigneeUserIds", userIds, {
                  shouldValidate: true,
                })
              }
            />
            <Field label="Title" required>
              <Input {...form.register("title")} />
            </Field>
            <Field label="Priority" required>
              <Select {...form.register("priority")}>
                {enumCodes.priority.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </Select>
            </Field>
            <Field label="Scheduled start" required>
              <Input
                type="datetime-local"
                {...form.register("scheduledStartAt")}
              />
            </Field>
            <Field
              label="Due"
              error={form.formState.errors.dueAt?.message}
              required
            >
              <Input type="datetime-local" {...form.register("dueAt")} />
            </Field>
            <div className="md:col-span-2">
              <Field label="Description">
                <Textarea {...form.register("description")} />
              </Field>
            </div>
            <label className="flex items-center gap-2 text-sm font-semibold md:col-span-2">
              <input type="checkbox" {...form.register("requiresApproval")} />
              Requires approval
            </label>
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
              Create task
            </Button>
          </div>
        </form>
      )}
    </>
  );
}

export async function generateAndSavePdf(task: Schemas["TaskDto"], locale: string) {
  const statusCode = enumCode("taskStatus", task.status);

  const questions = task.items.filter(
    (i) => (i.itemType ? enumCode("taskItemType", i.itemType) : "Question") === "Question"
  );
  const yesCount = questions.filter(
    (i) => i.value === "Yes" || i.value === "Да"
  ).length;
  const noCount = questions.length - yesCount;
  const yesPercentage =
    questions.length > 0 ? Math.round((yesCount / questions.length) * 100) : 100;

  const totalItems = task.items.length;
  const completedCount = task.items.filter(
    (i) => enumCode("taskItemStatus", i.status) === "Completed" || i.value !== null
  ).length;
  const completionPercentage =
    totalItems > 0 ? Math.round((completedCount / totalItems) * 100) : 0;

  const totalPhotos = task.items.reduce(
    (acc, item) => acc + ((item.attachments?.length ?? 0) || Number(item.attachmentCount) || 0),
    0
  );

  const reportStats = {
    questionsCount: questions.length,
    yesCount,
    noCount,
    yesPercentage,
    totalItems,
    completedCount,
    completionPercentage,
    totalPhotos,
  };

  const blocksMap = new Map<string, Map<string, Schemas["TaskItemDto"][]>>();
  for (const item of task.items) {
    const main = item.mainBlockTitle?.trim() || "";
    const sub = item.subBlockTitle?.trim() || "";
    if (!blocksMap.has(main)) {
      blocksMap.set(main, new Map());
    }
    const subs = blocksMap.get(main)!;
    if (!subs.has(sub)) {
      subs.set(sub, []);
    }
    subs.get(sub)!.push(item);
  }

  let mainCounter = 0;
  const itemGroups = Array.from(blocksMap.entries()).map(([main, subs]) => {
    const hasMain = Boolean(main);
    if (hasMain) mainCounter++;
    const mainPrefix = hasMain ? `${mainCounter}.` : "";

    let subCounter = 0;
    const subBlocks = Array.from(subs.entries()).map(([sub, items]) => {
      const hasSub = Boolean(sub);
      if (hasSub) subCounter++;
      const subPrefix = hasSub
        ? hasMain
          ? `${mainCounter}.${subCounter}.`
          : `${subCounter}.`
        : "";

      let itemCounter = 0;
      const numberedItems = items.map((item) => {
        itemCounter++;
        let itemPrefix = `${itemCounter}.`;
        if (hasMain && hasSub) {
          itemPrefix = `${mainCounter}.${subCounter}.${itemCounter}.`;
        } else if (hasMain && !hasSub) {
          itemPrefix = `${mainCounter}.${itemCounter}.`;
        } else if (!hasMain && hasSub) {
          itemPrefix = `${subCounter}.${itemCounter}.`;
        }
        return { item, itemPrefix };
      });

      return {
        subBlock: sub,
        subPrefix,
        items: numberedItems,
      };
    });

    return {
      mainBlock: main,
      mainPrefix,
      subBlocks,
    };
  });

  let itemsHtml = "";
  itemGroups.forEach((group) => {
    if (group.mainBlock) {
      itemsHtml += `<div style="font-size:15px; font-weight:900; color:#1e1b4b; border-bottom:2px solid #e2e8f0; padding-bottom:4px; margin-top:20px; margin-bottom:10px;">${group.mainPrefix} ${group.mainBlock}</div>`;
    }
    group.subBlocks.forEach((sub) => {
      if (sub.subBlock) {
        itemsHtml += `<div style="font-size:13px; font-weight:800; color:#334155; margin-top:12px; margin-bottom:6px; padding-left:4px;">${sub.subPrefix} ${sub.subBlock}</div>`;
      }
      sub.items.forEach(({ item, itemPrefix }) => {
        const completed = enumCode("taskItemStatus", item.status) === "Completed";
        const itemType = item.itemType ? enumCode("taskItemType", item.itemType) : "Question";
        const attachments = (item as any).attachments ?? [];

        let responseHtml = "";
        if (itemType === "Question") {
          const isYes = item.value === "Yes" || item.value === "Да";
          const isNo = item.value === "No" || item.value === "Нет";
          if (isYes) responseHtml = '<span style="display:inline-block; padding:3px 10px; border-radius:9999px; font-size:11px; font-weight:800; background:#dcfce7; color:#166534;">✓ Да (Yes)</span>';
          else if (isNo) responseHtml = '<span style="display:inline-block; padding:3px 10px; border-radius:9999px; font-size:11px; font-weight:800; background:#ffe4e6; color:#9f1239;">✗ Нет (No)</span>';
          else responseHtml = '<span style="display:inline-block; padding:3px 10px; border-radius:9999px; font-size:11px; font-weight:800; background:#ffe4e6; color:#9f1239;">✗ Нет ответа</span>';
        } else if (item.value) {
          responseHtml = `<div style="font-size:12px; font-weight:700; color:#0f172a;"><strong>Ответ:</strong> ${item.value}</div>`;
        } else {
          responseHtml = '<div style="font-size:12px; color:#94a3b8;"><strong>Ответ:</strong> —</div>';
        }

        let attHtml = "";
        if (attachments.length > 0) {
          attHtml = '<div style="margin-top:12px; padding-top:10px; border-top:1px solid #e2e8f0;"><div style="font-size:11px; font-weight:800; text-transform:uppercase; color:#4f46e5; margin-bottom:8px;">📷 Прикрепленные фото и доказательства (' + attachments.length + '):</div><div style="display:flex; flex-wrap:wrap; gap:12px;">';
          attachments.forEach((att: any) => {
            const fileTypeLower = (att.fileType || "").toLowerCase();
            const urlLower = (att.fileUrl || "").toLowerCase();
            const isImage = fileTypeLower.startsWith("image/") || /\.(png|jpe?g|webp|gif|svg)($|\?)/.test(urlLower);
            if (isImage && att.fileUrl) {
              attHtml += `<div style="border:1px solid #cbd5e1; border-radius:10px; overflow:hidden; background:#ffffff; padding:6px; page-break-inside:avoid;"><img src="${att.fileUrl}" style="width:280px; max-width:100%; height:190px; object-fit:cover; border-radius:8px; display:block;" alt="Evidence Photo" /><div style="font-size:10px; font-weight:700; color:#64748b; margin-top:4px; text-align:center; max-width:280px; text-overflow:ellipsis; overflow:hidden;">${att.fileName || "Фото доказательства"}</div></div>`;
            } else if (att.fileUrl) {
              attHtml += `<a href="${att.fileUrl}" target="_blank" style="font-size:11px; color:#4f46e5; font-weight:bold; padding:8px 12px; background:#e0e7ff; border-radius:6px; text-decoration:none; display:inline-block;">📄 ${att.fileName || "Файл доказательства"} ↗</a>`;
            }
          });
          attHtml += '</div></div>';
        }

        itemsHtml += `
          <div style="border:1px solid #e2e8f0; border-radius:10px; padding:12px 16px; margin-bottom:10px; background:#ffffff; page-break-inside:avoid;">
            <div style="display:flex; justify-content:space-between; align-items:flex-start; gap:8px;">
              <div style="font-size:13px; font-weight:800; color:#0f172a;">${itemPrefix} ${item.title}</div>
              <span style="display:inline-block; padding:2px 8px; border-radius:9999px; font-size:10px; font-weight:800; background:${completed ? '#dcfce7' : '#fef3c7'}; color:${completed ? '#166534' : '#92400e'};">${completed ? '✓ Готово' : 'В процессе'}</span>
            </div>
            ${item.description ? `<div style="font-size:12px; color:#64748b; margin-top:3px;">${item.description}</div>` : ''}
            <div style="margin-top:6px;">${responseHtml}</div>
            ${item.note ? `<div style="font-size:12px; color:#475569; margin-top:6px; font-style:italic;">Заметка: ${item.note}</div>` : ''}
            ${attHtml}
          </div>
        `;
      });
    });
  });

  const fullReportHtml = `
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Task Report - ${task.title}</title>
  <style>
    @media print {
      body { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
      .no-print { display: none !important; }
    }
    body { font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; color: #0f172a; padding: 24px; background: #ffffff; max-width: 800px; margin: 0 auto; }
  </style>
</head>
<body>
  <div style="display:flex; justify-content:space-between; align-items:flex-start; border-bottom:3px solid #4f46e5; padding-bottom:14px; margin-bottom:18px;">
    <div>
      <div style="font-size:10px; font-weight:800; text-transform:uppercase; letter-spacing:1px; color:#4f46e5; margin-bottom:4px;">OPS MANAGER • EXECUTIVE REPORT</div>
      <h1 style="font-size:22px; font-weight:900; color:#0f172a; margin:0; line-height:1.2;">${task.title}</h1>
      <div style="font-size:12px; color:#64748b; margin-top:4px; font-weight:600;">Дата события: ${task.occurrenceDate || "—"}</div>
    </div>
    <div style="text-align:right;">
      <span style="display:inline-block; padding:6px 14px; border-radius:8px; font-size:12px; font-weight:800; background:#e0e7ff; color:#3730a3;">
        ${statusCode}
      </span>
    </div>
  </div>

  <div style="display:grid; grid-template-columns:repeat(4, 1fr); gap:10px; background:#f8fafc; border:1px solid #e2e8f0; border-radius:10px; padding:12px 14px; margin-bottom:18px;">
    <div>
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Отдел</div>
      <div style="font-size:12px; font-weight:800; color:#0f172a; margin-top:2px;">${task.departmentName || "—"}</div>
    </div>
    <div>
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Исполнитель</div>
      <div style="font-size:12px; font-weight:800; color:#0f172a; margin-top:2px;">${task.assigneeName || "—"}</div>
    </div>
    <div>
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Начало</div>
      <div style="font-size:12px; font-weight:800; color:#0f172a; margin-top:2px;">${formatDateTime(task.scheduledStartAt, locale)}</div>
    </div>
    <div>
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Срок (Due)</div>
      <div style="font-size:12px; font-weight:800; color:#0f172a; margin-top:2px;">${formatDateTime(task.dueAt, locale)}</div>
    </div>
  </div>

  <div style="display:grid; grid-template-columns:repeat(3, 1fr); gap:10px; margin-bottom:20px;">
    <div style="border:1px solid #cbd5e1; border-radius:10px; padding:12px; background:#fafafa;">
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Соответствие (Да)</div>
      <div style="font-size:22px; font-weight:900; color:${reportStats.yesPercentage >= 80 ? '#166534' : reportStats.yesPercentage >= 50 ? '#92400e' : '#9f1239'}; margin-top:2px;">${reportStats.yesPercentage}%</div>
      <div style="font-size:10px; color:#64748b; font-weight:600; margin-top:2px;">${reportStats.yesCount} из ${reportStats.questionsCount} вопросов Да</div>
    </div>
    <div style="border:1px solid #cbd5e1; border-radius:10px; padding:12px; background:#fafafa;">
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Завершено пунктов</div>
      <div style="font-size:22px; font-weight:900; color:#4f46e5; margin-top:2px;">${reportStats.completionPercentage}%</div>
      <div style="font-size:10px; color:#64748b; font-weight:600; margin-top:2px;">${reportStats.completedCount} из ${reportStats.totalItems} выполнено</div>
    </div>
    <div style="border:1px solid #cbd5e1; border-radius:10px; padding:12px; background:#fafafa;">
      <div style="font-size:9px; font-weight:800; text-transform:uppercase; color:#64748b;">Фото доказательства</div>
      <div style="font-size:22px; font-weight:900; color:#0f172a; margin-top:2px;">${reportStats.totalPhotos}</div>
      <div style="font-size:10px; color:#64748b; font-weight:600; margin-top:2px;">прикреплено файлов</div>
    </div>
  </div>

  <h2 style="font-size:15px; font-weight:900; color:#0f172a; margin-top:20px; margin-bottom:12px; border-bottom:2px solid #e2e8f0; padding-bottom:6px;">
    Результаты проверки чек-листа
  </h2>
  ${itemsHtml}
</body>
</html>
  `;

  // Method 1: Try html2pdf.js direct download
  try {
    const html2pdfModule = await import("html2pdf.js");
    const html2pdfFn = (html2pdfModule as any).default || html2pdfModule;

    if (typeof html2pdfFn === "function") {
      const container = document.createElement("div");
      container.style.position = "fixed";
      container.style.left = "0";
      container.style.top = "0";
      container.style.zIndex = "-99999";
      container.style.width = "790px";
      container.style.background = "#ffffff";
      container.innerHTML = fullReportHtml;
      document.body.appendChild(container);

      const imgElements = Array.from(container.querySelectorAll("img"));
      if (imgElements.length > 0) {
        await Promise.all(
          imgElements.map(
            (img) =>
              new Promise<void>((resolve) => {
                if (img.complete && img.naturalWidth > 0) {
                  resolve();
                } else {
                  img.onload = () => resolve();
                  img.onerror = () => resolve();
                }
              })
          )
        );
      }

      const cleanTitle = task.title.replace(/[^a-zA-Z0-9_\-\u0600-\u06FF\u0400-\u04FF]/g, "_").substring(0, 40);
      const opt = {
        margin: [8, 8, 8, 8] as [number, number, number, number],
        filename: `Task_Report_${cleanTitle}_${task.occurrenceDate || "report"}.pdf`,
        image: { type: "jpeg" as const, quality: 0.98 },
        html2canvas: {
          scale: 2,
          useCORS: true,
          logging: false,
          scrollY: 0,
          scrollX: 0,
          windowWidth: 850,
        },
        jsPDF: { unit: "mm" as const, format: "a4" as const, orientation: "portrait" as const },
        pagebreak: { mode: ["avoid-all", "css", "legacy"] },
      };

      await html2pdfFn().set(opt).from(container).save();
      document.body.removeChild(container);
      return;
    }
  } catch (pdfErr) {
    console.warn("html2pdf failed, falling back to print window:", pdfErr);
  }

  // Method 2: Fallback to print-to-PDF window if html2pdf fails or is blocked
  const printWindow = window.open("", "_blank");
  if (printWindow) {
    printWindow.document.write(fullReportHtml);
    printWindow.document.close();
    printWindow.focus();
    setTimeout(() => {
      printWindow.print();
    }, 400);
  }
}

export function TaskDetail({ id }: { id: string }) {
  const locale = useLocale();
  const tCommon = useTranslations("Common");
  const { identity } = useAuth();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const [actionError, setActionError] = useState<string>();
  const [isDownloadingPdf, setIsDownloadingPdf] = useState(false);
  const query = useQuery({
    queryKey: queryKeys.tasks.detail(id),
    queryFn: () => apiRequest<Schemas["TaskDto"]>(`/tasks/${id}`),
  });
  const task = query.data;
  const status = enumCode("taskStatus", task?.status);
  const windowState = enumCode("taskExecutionWindowState", task?.executionWindowState);

  const mutation = useMutation({
  
    mutationFn: ({
      action,
      body,
    }: {
      action: string;
      body?: Record<string, unknown>;
    }) =>

      apiRequest<Schemas["TaskDto"] | Schemas["TaskDistributionResponse"]>(
        `/tasks/${id}/${action}`,
        {
          method: "POST",
          body,
        }),
    onSuccess: (data) => {//
      console.log("mutation", mutation); ///
      if ("tasks" in data) {
        void queryClient.invalidateQueries({ queryKey: ["tasks"] });
        const firstTask = data.tasks[0];
        if (firstTask) router.push(`/tasks/${firstTask.taskId}`);
        return;
      }

      queryClient.setQueryData(queryKeys.tasks.detail(id), data);
      void queryClient.invalidateQueries({ queryKey: ["tasks"] });
      toast.push("Task updated.");
      setReason("");
    },
    onError: (error) => {
      setActionError(errorMessage(error));
      if (error instanceof ApiError && error.status === 409) {
        void query.refetch();
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async () => {
      await apiRequest(`/tasks/${id}`, { method: "DELETE" });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["tasks"] });
      toast.push("Task deleted.");
      router.push("/tasks/upcoming");
    },
    onError: (error) => {
      setActionError(errorMessage(error));
    },
  });

  async function updateItem(item: Schemas["TaskItemDto"], complete: boolean, val?: string) {
    setActionError(undefined);
    try {
      await apiRequest(`/tasks/${id}/items/${item.id}`, {
        method: "PATCH",
        body: { status: complete ? 1 : 0, note: item.note, value: val ?? item.value },
      });
      await query.refetch();
      toast.push("Checklist item updated.");
    } catch (error) {
      setActionError(errorMessage(error));
    }
  }

  async function uploadEvidence(itemId: string, file: File | null) {
    if (!file) return;
    const data = new FormData();
    data.append("file", file);
    setActionError(undefined);
    try {
      await apiRequest(`/tasks/${id}/items/${itemId}/attachments`, {
        method: "POST",
        body: data,
      });
      await query.refetch();
      toast.push("Evidence uploaded.");
    } catch (error) {
      setActionError(errorMessage(error));
    }
  }

  if (query.isLoading) return <Skeleton className="h-[34rem]" />;
  if (query.error || !task) {
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  }
  const writable = canMutateTenant(identity);
  const isOwner =
    identity?.realm === "tenant" &&
    identity.session.user.id === task.assigneeUserId;
  const role =
    identity?.realm === "tenant"
      ? enumCode("organizationRole", identity.session.membership.role)
      : "";
  const canManage = writable && (role === "Manager" || role === "Supervisor");
  const canExecute = writable && isOwner;
  const canEditItems = canExecute && status === "InProgress";
  const reasonBody = { reason: reason.trim() };

  // Group items by mainBlockTitle -> subBlockTitle with hierarchical numbering
  const itemGroups = (() => {
    const blocksMap = new Map<string, Map<string, Schemas["TaskItemDto"][]>>();
    for (const item of task.items) {
      const main = item.mainBlockTitle?.trim() || "";
      const sub = item.subBlockTitle?.trim() || "";
      if (!blocksMap.has(main)) {
        blocksMap.set(main, new Map());
      }
      const subs = blocksMap.get(main)!;
      if (!subs.has(sub)) {
        subs.set(sub, []);
      }
      subs.get(sub)!.push(item);
    }

    let mainCounter = 0;
    return Array.from(blocksMap.entries()).map(([main, subs]) => {
      const hasMain = Boolean(main);
      if (hasMain) mainCounter++;
      const mainPrefix = hasMain ? `${mainCounter}.` : "";

      let subCounter = 0;
      const subBlocks = Array.from(subs.entries()).map(([sub, items]) => {
        const hasSub = Boolean(sub);
        if (hasSub) subCounter++;
        const subPrefix = hasSub
          ? hasMain
            ? `${mainCounter}.${subCounter}.`
            : `${subCounter}.`
          : "";

        let itemCounter = 0;
        const numberedItems = items.map((item) => {
          itemCounter++;
          let itemPrefix = `${itemCounter}.`;
          if (hasMain && hasSub) {
            itemPrefix = `${mainCounter}.${subCounter}.${itemCounter}.`;
          } else if (hasMain && !hasSub) {
            itemPrefix = `${mainCounter}.${itemCounter}.`;
          } else if (!hasMain && hasSub) {
            itemPrefix = `${subCounter}.${itemCounter}.`;
          }
          return { item, itemPrefix };
        });

        return {
          subBlock: sub,
          subPrefix,
          items: numberedItems,
        };
      });

      return {
        mainBlock: main,
        mainPrefix,
        subBlocks,
      };
    });
  })();

  const isOverdue = (() => {
    const isTerminal = status === "Completed" || status === "Cancelled";
    if (isTerminal) return false;
    return new Date(task.dueAt).getTime() < Date.now();
  })();

  const isReportMode =
    (status !== "NotStarted" && status !== "InProgress") || !canExecute;

  const reportStats = (() => {
    const questions = task.items.filter(
      (i) => (i.itemType ? enumCode("taskItemType", i.itemType) : "Question") === "Question"
    );
    const yesCount = questions.filter(
      (i) => i.value === "Yes" || i.value === "Да"
    ).length;
    const noCount = questions.length - yesCount;
    const yesPercentage =
      questions.length > 0 ? Math.round((yesCount / questions.length) * 100) : 100;

    const totalItems = task.items.length;
    const completedCount = task.items.filter(
      (i) => enumCode("taskItemStatus", i.status) === "Completed" || i.value !== null
    ).length;
    const completionPercentage =
      totalItems > 0 ? Math.round((completedCount / totalItems) * 100) : 0;

    const totalPhotos = task.items.reduce(
      (acc, item) => acc + ((item.attachments?.length ?? 0) || Number(item.attachmentCount) || 0),
      0
    );

    return {
      questionsCount: questions.length,
      yesCount,
      noCount,
      yesPercentage,
      totalItems,
      completedCount,
      completionPercentage,
      totalPhotos,
    };
  })();

  const isManagerUser = isManager(identity);

  async function handleDownloadReport() {
    if (!task) return;
    setIsDownloadingPdf(true);
    toast.push("Генерация PDF отчета...");
    try {
      await generateAndSavePdf(task, locale);
    } catch (err) {
      console.error("Failed to generate PDF:", err);
      toast.push(`Ошибка PDF: ${errorMessage(err)}`, "danger");
    } finally {
      setIsDownloadingPdf(false);
    }
  }

  return (
    <>
      {isOverdue ? (
        <div className="mb-4">
          <Alert tone="danger">
            <div className="flex items-center gap-2">
              <Clock className="size-5 shrink-0 text-rose-600" />
              <div>
                <strong className="font-bold">Задача просрочена (Missed Task)!</strong>
                <p className="mt-0.5 text-xs">
                  Срок выполнения этой задачи истек ({formatDateTime(task.dueAt, locale)}), но она не была завершена в срок.
                </p>
              </div>
            </div>
          </Alert>
        </div>
      ) : null}
      <PageHeader
        title={task.title}
        description={`${formatDateTime(task.scheduledStartAt, locale)} → ${formatDateTime(task.dueAt, locale)}`}
        actions={
          <>
            {isOverdue ? (
              <Badge tone="danger">Просрочено (Missed)</Badge>
            ) : null}
            <Badge tone={statusTone(status)}>{status}</Badge>
            {isManagerUser ? (
              <Button
                variant="secondary"
                busy={isDownloadingPdf}
                onClick={() => void handleDownloadReport()}
                className="inline-flex items-center gap-2"
              >
                <Download className="size-4 text-indigo-600" /> Скачать PDF
              </Button>
            ) : null}
            {canManage ? (
              <Button
                variant="danger"
                busy={deleteMutation.isPending}
                onClick={() => {
                  if (confirm(tCommon("deleteTask") + "?")) {
                    deleteMutation.mutate();
                  }
                }}
              >
                <Trash2 className="size-4" /> {tCommon("deleteTask")}
              </Button>
            ) : null}
            {status === "NotStarted" ? (
              <Button
                disabled={!canExecute || !task.canStart}
                busy={mutation.isPending}
                onClick={() => mutation.mutate({ action: "start" })}
              >
                Start
              </Button>
            ) : null}
            {status === "Blocked" || status === "Returned" ? (
              <Button
                disabled={!canExecute}
                onClick={() => mutation.mutate({ action: "resume" })}
              >
                Resume
              </Button>
            ) : null}
            {status === "InProgress" ? (
              <Button
                disabled={!canExecute || !task.canComplete}
                onClick={() =>
                  mutation.mutate({
                    action: task.requiresApproval
                      ? "submit-for-approval"
                      : "complete",
                  })
                }
              >
                {task.requiresApproval ? "Submit for approval" : "Complete"}
              </Button>
            ) : null}
            {status === "PendingApproval" ? (
              <Button
                disabled={!canManage}
                onClick={() => mutation.mutate({ action: "approve" })}
              >
                Approve
              </Button>
            ) : null}
          </>
        }
      />
      {status === "Cancelled" && task.cancellationReason ? (
        <div className="mt-3">
          <Alert tone="danger">
            <span className="font-bold">{tCommon("cancellationReason")}: </span>
            {task.cancellationReason}
          </Alert>
        </div>
      ) : null}
      {windowState === "NotOpen" ? (
        <div className="mt-3">
          <Alert tone="warning">
            {tCommon("taskNotOpenYet", {
              time: formatDateTime(task.scheduledStartAt, locale),
            })}
          </Alert>
        </div>
      ) : windowState === "Expired" ? (
        <div className="mt-3">
          <Alert tone="warning">{tCommon("taskWindowExpired")}</Alert>
        </div>
      ) : null}
      {actionError || mutation.error ? (
        <div className="mt-3">
          <Alert tone="danger">
            {actionError ?? errorMessage(mutation.error)}
          </Alert>
        </div>
      ) : null}
      <div className="mt-5 grid gap-5 xl:grid-cols-[1fr_20rem]">
        <div className="grid gap-5">
          {/* Executive Task Report & Compliance Statistics Card */}
          {status !== "NotStarted" && status !== "InProgress" ? (
            <Card className="border-indigo-100 bg-gradient-to-br from-indigo-50/40 via-white to-sky-50/30 p-5 shadow-xs">
              <div className="flex flex-wrap items-center justify-between gap-4 border-b border-indigo-100 pb-4">
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-xl font-black text-slate-900">
                      {isReportMode ? "Отчет по задаче (Task Report)" : "Прогресс выполнения"}
                    </h2>
                    <Badge
                      tone={
                        reportStats.yesPercentage >= 80
                          ? "success"
                          : reportStats.yesPercentage >= 50
                          ? "warning"
                          : "danger"
                      }
                    >
                      {reportStats.yesPercentage >= 80
                        ? "Высокое соответствие"
                        : reportStats.yesPercentage >= 50
                        ? "Среднее соответствие"
                        : "Низкое соответствие"}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs font-semibold text-slate-600">
                    {isReportMode
                      ? "Аналитический отчет и результаты проверок по задаче"
                      : "Режим заполнения чек-листа задачи"}
                  </p>
                </div>
                <div className="text-end">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    Результат "Да (Yes)"
                  </span>
                  <p
                    className={`text-2xl font-black ${
                      reportStats.yesPercentage >= 80
                        ? "text-emerald-600"
                        : reportStats.yesPercentage >= 50
                        ? "text-amber-600"
                        : "text-rose-600"
                    }`}
                  >
                    {reportStats.yesPercentage}%
                  </p>
                </div>
              </div>

              <div className="mt-4 grid gap-4 sm:grid-cols-3">
                {/* Question Yes Percentage Card */}
                <div className="rounded-xl border border-slate-200/80 bg-white p-3.5 shadow-2xs">
                  <p className="text-xs font-bold uppercase tracking-wider text-slate-600">
                    Процент ответов "Да":
                  </p>
                  <div className="mt-2 flex items-baseline justify-between">
                    <span className="text-xl font-black text-slate-900">
                      {reportStats.yesPercentage}%
                    </span>
                    <span className="text-xs font-bold text-slate-600">
                      {reportStats.yesCount} из {reportStats.questionsCount} Да
                    </span>
                  </div>
                  <div className="mt-2 h-2.5 w-full overflow-hidden rounded-full bg-slate-100">
                    <div
                      className={`h-full transition-all ${
                        reportStats.yesPercentage >= 80
                          ? "bg-emerald-500"
                          : reportStats.yesPercentage >= 50
                          ? "bg-amber-500"
                          : "bg-rose-500"
                      }`}
                      style={{ width: `${reportStats.yesPercentage}%` }}
                    />
                  </div>
                  <p className="mt-1.5 text-[11px] font-medium text-slate-500">
                    * Неотвеченные пункты считаются как "Нет"
                  </p>
                </div>

                {/* Completion Percentage Card */}
                <div className="rounded-xl border border-slate-200/80 bg-white p-3.5 shadow-2xs">
                  <p className="text-xs font-bold uppercase tracking-wider text-slate-600">
                    Завершено пунктов:
                  </p>
                  <div className="mt-2 flex items-baseline justify-between">
                    <span className="text-xl font-black text-slate-900">
                      {reportStats.completionPercentage}%
                    </span>
                    <span className="text-xs font-bold text-slate-600">
                      {reportStats.completedCount} из {reportStats.totalItems} готово
                    </span>
                  </div>
                  <div className="mt-2 h-2.5 w-full overflow-hidden rounded-full bg-slate-100">
                    <div
                      className="h-full bg-indigo-600 transition-all"
                      style={{ width: `${reportStats.completionPercentage}%` }}
                    />
                  </div>
                </div>

                {/* Evidence & Attachments Card */}
                <div className="rounded-xl border border-slate-200/80 bg-white p-3.5 shadow-2xs">
                  <p className="text-xs font-bold uppercase tracking-wider text-slate-600">
                    Загружено фото / доказательств:
                  </p>
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-xl font-black text-slate-900">
                      {reportStats.totalPhotos}
                    </span>
                    <Badge tone={reportStats.totalPhotos > 0 ? "success" : "neutral"}>
                      {reportStats.totalPhotos > 0 ? "Есть доказательства" : "Нет фото"}
                    </Badge>
                  </div>
                  <p className="mt-2 text-xs font-medium text-slate-500">
                    Файлов прикреплено к задаче
                  </p>
                </div>
              </div>
            </Card>
          ) : null}

          <Card className="p-4">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("department")}</p>
                <p className="font-bold text-ink-900">{task.departmentName || "—"}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("employee")}</p>
                <p className="font-bold text-ink-900">{task.assigneeName || "—"}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("occurrenceDate")}</p>
                <p className="font-bold text-ink-900">{task.occurrenceDate}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("scheduledStartAt")}</p>
                <p className="font-bold text-ink-900">{formatDateTime(task.scheduledStartAt, locale)}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("dueAt")}</p>
                <p className="font-bold text-ink-900">{formatDateTime(task.dueAt, locale)}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("priority")}</p>
                <Badge tone={task.priority === 3 ? "danger" : task.priority === 2 ? "warning" : "neutral"}>
                  {enumCode("priority", task.priority)}
                </Badge>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("requiresApproval")}</p>
                <p className="font-bold text-ink-900">{task.requiresApproval ? tCommon("yes") : tCommon("no")}</p>
              </div>
              <div>
                <p className="text-ink-500 text-xs font-semibold uppercase">{tCommon("isOverdue")}</p>
                <Badge tone={task.isOverdue ? "danger" : "success"}>
                  {task.isOverdue ? tCommon("yes") : tCommon("no")}
                </Badge>
              </div>
            </div>
          </Card>

          <Card>
            <h2 className="mb-4 text-lg font-black">
              {isReportMode ? "Отчет по чек-листу (Checklist Report)" : "Чек-лист задачи"}
            </h2>
            {status === "NotStarted" && canExecute ? (
              <div className="mb-4">
                <Alert tone="info">
                  {task.canStart
                    ? "Чтобы начать выполнять пункты задачи, нажмите кнопку «Начать выполнение» выше."
                    : "Эта задача станет доступна для выполнения в указанное время начала."}
                </Alert>
              </div>
            ) : null}
            <div className="grid gap-5">
              {itemGroups.map((group, groupIdx) => (
                <div key={groupIdx} className="grid gap-3">
                  {group.mainBlock ? (
                    <h3 className="border-b pb-2 text-base font-black text-ink-900 flex items-center gap-2">
                      <span className="text-brand-700 font-extrabold">{group.mainPrefix}</span>
                      <span>{group.mainBlock}</span>
                    </h3>
                  ) : null}
                  {group.subBlocks.map((sub, subIdx) => (
                    <div key={subIdx} className="grid gap-3 ps-2">
                      {sub.subBlock ? (
                        <h4 className="text-sm font-bold text-ink-700 flex items-center gap-2">
                          <span className="text-brand-700 font-bold">{sub.subPrefix}</span>
                          <span>{sub.subBlock}</span>
                        </h4>
                      ) : null}
                      {sub.items.map(({ item, itemPrefix }) => {
                        const completed =
                          enumCode("taskItemStatus", item.status) === "Completed";
                        const evidence = enumCode("evidenceMode", item.evidenceMode);
                        const itemType = item.itemType
                          ? enumCode("taskItemType", item.itemType)
                          : "Question";

                        const itemAttachments = (item as any).attachments ?? [];

                        return (
                          <div
                            key={item.id}
                            className="border-ink-950/10 rounded-xl border p-4 bg-surface-50/50"
                          >
                            <div className="flex items-start gap-3">
                              <div className="min-w-0 flex-1">
                                <div className="flex items-center justify-between gap-2">
                                  <p className="font-bold text-ink-900 text-base flex items-baseline gap-1.5">
                                    <span className="text-brand-700 font-extrabold shrink-0">{itemPrefix}</span>
                                    <span>{item.title}</span>
                                  </p>
                                  {completed ? (
                                    <span className="shrink-0 flex items-center gap-1">
                                      <Badge tone="success">
                                        ✓ Completed
                                      </Badge>
                                    </span>
                                  ) : null}
                                </div>
                                {item.description ? (
                                  <p className="text-ink-600 mt-1 text-sm">
                                    {item.description}
                                  </p>
                                ) : null}

                                {/* Item Dynamic Input per ItemType or Read-Only Report Display */}
                                {itemType === "Question" ? (
                                  isReportMode ? (
                                    <div className="mt-3 flex items-center gap-3 bg-white p-3 rounded-lg border border-ink-950/10 max-w-sm">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Ответ:
                                      </span>
                                      {item.value === "Yes" || item.value === "Да" ? (
                                        <Badge tone="success">
                                          ✓ Да (Yes)
                                        </Badge>
                                      ) : item.value === "No" || item.value === "Нет" ? (
                                        <Badge tone="danger">
                                          ✗ Нет (No)
                                        </Badge>
                                      ) : (
                                        <Badge tone="danger">
                                          ✗ Нет ответа (Считается как Нет)
                                        </Badge>
                                      )}
                                    </div>
                                  ) : (
                                    <div className="mt-3 flex flex-col gap-2.5 bg-white p-3 rounded-lg border border-ink-950/10 max-w-sm">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Выберите ответ:
                                      </span>
                                      <label className="flex items-center gap-3 cursor-pointer text-sm font-semibold text-ink-900 hover:text-sky-600 transition-colors">
                                        <input
                                          type="checkbox"
                                          className="size-4 rounded text-sky-600 focus:ring-sky-500"
                                          checked={item.value === "Yes" || item.value === "Да"}
                                          disabled={!canEditItems}
                                          onChange={() => void updateItem(item, true, "Yes")}
                                        />
                                        Да (Yes)
                                      </label>
                                      <label className="flex items-center gap-3 cursor-pointer text-sm font-semibold text-ink-900 hover:text-rose-600 transition-colors">
                                        <input
                                          type="checkbox"
                                          className="size-4 rounded text-rose-600 focus:ring-rose-500"
                                          checked={item.value === "No" || item.value === "Нет"}
                                          disabled={!canEditItems}
                                          onChange={() => void updateItem(item, true, "No")}
                                        />
                                        Нет (No)
                                      </label>
                                    </div>
                                  )
                                ) : itemType === "RatingSlider" ? (
                                  isReportMode ? (
                                    <div className="mt-3 flex items-center gap-3 bg-white p-3 rounded-lg border border-ink-950/10 max-w-sm">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Оценка:
                                      </span>
                                      <Badge tone="warning">
                                        {item.value !== null && item.value !== undefined
                                          ? `Балл: ${item.value} / 10`
                                          : "Без оценки"}
                                      </Badge>
                                    </div>
                                  ) : (
                                    <div className="mt-3 max-w-lg bg-white p-3 rounded-lg border border-ink-950/10">
                                      <div className="flex justify-between text-xs font-semibold mb-2 text-ink-700">
                                        <span>Оценка (от 0 до 10):</span>
                                        {item.value !== null && item.value !== undefined ? (
                                          <span className="font-bold text-amber-600">
                                            Балл: {item.value} / 10
                                          </span>
                                        ) : null}
                                      </div>
                                      <div className="flex flex-wrap gap-1.5">
                                        {[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((score) => (
                                          <button
                                            key={score}
                                            type="button"
                                            disabled={!canEditItems}
                                            onClick={() => void updateItem(item, true, String(score))}
                                            className={`size-8 rounded-lg text-xs font-bold transition-all ${
                                              String(item.value) === String(score)
                                                ? "bg-amber-500 text-white shadow-sm scale-105"
                                                : "bg-surface-200 text-ink-700 hover:bg-surface-300"
                                            }`}
                                          >
                                            {score}
                                          </button>
                                        ))}
                                      </div>
                                    </div>
                                  )
                                ) : itemType === "MultiLineText" ? (
                                  isReportMode ? (
                                    <div className="mt-3 max-w-xl bg-white p-3 rounded-lg border border-ink-950/10">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500 block mb-1">
                                        Текстовый ответ:
                                      </span>
                                      <p className="text-sm font-medium text-ink-900 whitespace-pre-wrap">
                                        {item.value || "Ответ не введен"}
                                      </p>
                                    </div>
                                  ) : (
                                    <div className="mt-3 grid gap-2 max-w-xl">
                                      <Textarea
                                        placeholder="Введите текстовый ответ..."
                                        defaultValue={item.value ?? ""}
                                        disabled={!canEditItems}
                                        id={`text-${item.id}`}
                                      />
                                      {canEditItems ? (
                                        <div>
                                          <Button
                                            type="button"
                                            size="sm"
                                            onClick={() => {
                                              const el = document.getElementById(`text-${item.id}`) as HTMLTextAreaElement;
                                              void updateItem(item, true, el?.value || "");
                                            }}
                                          >
                                            <FileCheck className="size-4" /> Сохранить ответ
                                          </Button>
                                        </div>
                                      ) : null}
                                    </div>
                                  )
                                ) : itemType === "SingleLineText" ? (
                                  isReportMode ? (
                                    <div className="mt-3 flex items-center gap-3 bg-white p-3 rounded-lg border border-ink-950/10 max-w-md">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Ответ:
                                      </span>
                                      <span className="text-sm font-bold text-ink-900">
                                        {item.value || "—"}
                                      </span>
                                    </div>
                                  ) : (
                                    <div className="mt-3 flex items-center gap-2 max-w-md">
                                      <Input
                                        placeholder="Введите ответ..."
                                        defaultValue={item.value ?? ""}
                                        disabled={!canEditItems}
                                        id={`text-${item.id}`}
                                      />
                                      {canEditItems ? (
                                        <Button
                                          type="button"
                                          size="sm"
                                          onClick={() => {
                                            const el = document.getElementById(`text-${item.id}`) as HTMLInputElement;
                                            void updateItem(item, true, el?.value || "");
                                          }}
                                        >
                                          Сохранить
                                        </Button>
                                      ) : null}
                                    </div>
                                  )
                                ) : itemType === "MultipleChoice" ? (
                                  isReportMode ? (
                                    <div className="mt-3 flex items-center gap-3 bg-white p-3 rounded-lg border border-ink-950/10 max-w-md">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Выбранный вариант:
                                      </span>
                                      <Badge tone="info">
                                        {item.value || "Вариант не выбран"}
                                      </Badge>
                                    </div>
                                  ) : (
                                    <div className="mt-3 flex flex-col gap-2 bg-white p-3 rounded-lg border border-ink-950/10 max-w-md">
                                      <span className="text-xs font-bold uppercase tracking-wider text-ink-500">
                                        Выберите вариант:
                                      </span>
                                      {(item.options
                                        ? item.options.split(",").map((o) => o.trim())
                                        : ["Вариант 1", "Вариант 2"]
                                      ).map((opt) => (
                                        <label
                                          key={opt}
                                          className="flex items-center gap-3 cursor-pointer text-sm font-semibold text-ink-900 hover:text-sky-600 transition-colors py-1"
                                        >
                                          <input
                                            type="radio"
                                            name={`mc-${item.id}`}
                                            className="size-4 text-sky-600 focus:ring-sky-500"
                                            checked={item.value === opt}
                                            disabled={!canEditItems}
                                            onChange={() => void updateItem(item, true, opt)}
                                          />
                                          {opt}
                                        </label>
                                      ))}
                                    </div>
                                  )
                                ) : itemType === "Instruction" ? (
                                  <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-sky-50 border border-sky-200 p-3 text-sky-900 text-sm">
                                    <div>
                                      <p className="font-bold">Инструкция:</p>
                                      <p className="mt-0.5">{item.description || item.title}</p>
                                    </div>
                                    {canEditItems && !completed ? (
                                      <Button
                                        type="button"
                                        size="sm"
                                        onClick={() => void updateItem(item, true, "Read")}
                                      >
                                        <Check className="size-4" /> Ознакомлен
                                      </Button>
                                    ) : completed ? (
                                      <Badge tone="success">Ознакомлен</Badge>
                                    ) : null}
                                  </div>
                                ) : null}

                                <div className="mt-2 flex flex-wrap gap-2">
                                  {item.isRequired ? (
                                    <Badge tone="warning">Required</Badge>
                                  ) : null}
                                  <Badge>{evidence} evidence</Badge>
                                  <Badge>
                                    <Paperclip className="size-3" />{" "}
                                    {String(itemAttachments.length || item.attachmentCount)}
                                  </Badge>
                                </div>

                                {/* Photo & Evidence Image Gallery Preview */}
                                {itemAttachments && itemAttachments.length > 0 ? (
                                  <div className="mt-3 pt-3 border-t border-ink-950/10">
                                    <p className="text-xs font-bold text-ink-700 mb-2 flex items-center gap-1.5">
                                      <Paperclip className="size-3.5 text-indigo-600" />
                                      Загруженные фото и доказательства ({itemAttachments.length}):
                                    </p>
                                    <div className="flex flex-wrap gap-3">
                                      {itemAttachments.map((att: any) => {
                                        const fileTypeLower = (att.fileType || "").toLowerCase();
                                        const urlLower = (att.fileUrl || "").toLowerCase();
                                        const isImage =
                                          fileTypeLower.startsWith("image/") ||
                                          /\.(png|jpe?g|webp|gif|svg)($|\?)/.test(urlLower);
                                        return (
                                          <a
                                            key={att.id}
                                            href={att.fileUrl}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            className="group relative block overflow-hidden rounded-xl border border-ink-950/15 bg-white p-1 shadow-2xs hover:border-indigo-500 hover:shadow-md transition-all"
                                          >
                                            {isImage ? (
                                              <img
                                                src={att.fileUrl}
                                                alt="Uploaded Evidence"
                                                className="size-24 object-cover rounded-lg group-hover:scale-105 transition-transform"
                                              />
                                            ) : (
                                              <div className="size-24 flex flex-col items-center justify-center bg-surface-100 rounded-lg p-2 text-center">
                                                <Paperclip className="size-6 text-ink-500 mb-1" />
                                                <span className="text-[11px] font-bold text-ink-700 truncate max-w-full">
                                                  Файл
                                                </span>
                                              </div>
                                            )}
                                            <span className="mt-1 block text-[10px] font-bold text-indigo-600 text-center truncate max-w-24">
                                              Просмотреть ↗
                                            </span>
                                          </a>
                                        );
                                      })}
                                    </div>
                                  </div>
                                ) : null}
                              </div>
                            </div>
                            {evidence !== "None" && !isReportMode ? (
                              <div className="mt-3">
                                <FileUploader
                                  label="Add evidence"
                                  disabled={!canEditItems}
                                  onChange={(file) =>
                                    void uploadEvidence(item.id, file)
                                  }
                                />
                              </div>
                            ) : null}
                          </div>
                        );
                      })}
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </Card>
        </div>
        <div className="grid content-start gap-5">
          <Card>
            <h2 className="mb-3 text-lg font-black">Workflow</h2>
            <Field
              label="Reason"
              hint="Required for block, return, and cancel."
            >
              <Textarea
                className="min-h-20"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </Field>
            <div className="mt-3 grid gap-2">
              {status === "InProgress" ? (
                <Button
                  variant="secondary"
                  disabled={!canExecute || !reason.trim()}
                  onClick={() =>
                    mutation.mutate({ action: "block", body: reasonBody })
                  }
                >
                  Block
                </Button>
              ) : null}
              {status === "PendingApproval" ? (
                <Button
                  variant="secondary"
                  disabled={!canManage || !reason.trim()}
                  onClick={() =>
                    mutation.mutate({ action: "return", body: reasonBody })
                  }
                >
                  Return
                </Button>
              ) : null}
              {!["Completed", "Cancelled"].includes(status) ? (
                <Button
                  variant="danger"
                  disabled={(!canExecute && !canManage) || !reason.trim()}
                  onClick={() =>
                    mutation.mutate({ action: "cancel", body: reasonBody })
                  }
                >
                  Cancel task
                </Button>
              ) : null}
              <Button
                variant="secondary"
                disabled={!canManage || !task.assigneeUserId}
                onClick={() =>
                  mutation.mutate({
                    action: "clone",
                    body: {
                      assignment: {
                        mode: enumValue("taskAssignmentMode", "SingleUser"),
                        userIds: task.assigneeUserId
                          ? [task.assigneeUserId]
                          : [],
                      },
                    },
                  })
                }
              >
                Clone task
              </Button>
            </div>
          </Card>
          <Card>
            <h2 className="mb-3 text-lg font-black">Milestones</h2>
            <dl className="grid gap-3 text-sm">
              {[
                ["Started", task.startedAt],
                ["Completed", task.completedAt],
                ["Approved", task.approvedAt],
              ].map(([label, value]) => (
                <div key={label}>
                  <dt className="text-ink-600 text-xs font-bold">{label}</dt>
                  <dd>{formatDateTime(value, locale)}</dd>
                </div>
              ))}
            </dl>
            <p className="text-ink-600 mt-4 text-xs">
              The current API exposes milestone timestamps but not a complete
              task-history feed.
            </p>
          </Card>
        </div>
      </div>
    </>
  );
}

function TaskDateGroup({
  dateLabel,
  tasks,
  defaultOpen,
  references,
  locale,
  router,
  tCommon,
  scope,
  isManagerUser,
}: {
  dateLabel: string;
  tasks: Schemas["TaskDto"][];
  defaultOpen: boolean;
  references: ReturnType<typeof useReferenceData>;
  locale: string;
  router: ReturnType<typeof useRouter>;
  tCommon: ReturnType<typeof useTranslations>;
  scope?: "Upcoming" | "Past";
  isManagerUser?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(defaultOpen);
  const [downloadingTaskId, setDownloadingTaskId] = useState<string | null>(null);

  useEffect(() => {
    setIsOpen(defaultOpen);
  }, [defaultOpen]);

  return (
    <Card className="mb-4 overflow-hidden p-0">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="border-ink-950/10 hover:bg-ink-950/5 flex w-full items-center justify-between border-b px-4 py-3 text-start font-bold transition-colors"
      >
        <div className="flex items-center gap-3">
          <span className="text-base font-extrabold text-ink-900">{dateLabel}</span>
          <Badge tone="info">
            {tasks.length} {tasks.length === 1 ? "task" : "tasks"}
          </Badge>
        </div>
        <div className="text-ink-500 flex items-center gap-1 text-xs font-normal">
          {isOpen ? (
            <ChevronDown className="size-5" />
          ) : (
            <ChevronRight className="size-5 rtl:rotate-180" />
          )}
        </div>
      </button>

      {isOpen && (
        <>
          {/* Desktop Table View */}
          <div className="hidden overflow-x-auto md:block">
            <table className="w-full border-collapse text-start text-sm">
              <thead>
                <tr className="border-ink-950/10 bg-ink-950/[0.025] border-b">
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("name")}
                  </th>
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("employeeDepartment")}
                  </th>
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("status")}
                  </th>
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("priority")}
                  </th>
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("startDateFrom")}
                  </th>
                  <th className="px-4 py-3 text-start font-bold">
                    {tCommon("startDateTo")}
                  </th>
                  {isManagerUser && scope === "Past" ? (
                    <th className="px-4 py-3 text-end font-bold">
                      PDF
                    </th>
                  ) : null}
                </tr>
              </thead>
              <tbody>
                {tasks.map((task) => {
                  const statusCode = enumCode("taskStatus", task.status);
                  const priorityCode = enumCode("priority", task.priority);
                  const empName = task.assigneeName ?? "—";
                  const dept = references.departments.find(
                    (d) => d.id === task.departmentId,
                  );
                  const deptName = task.departmentName ?? dept?.name ?? "—";
                  const employeeDeptText = `${empName} / ${deptName}`;
                  const isMissed =
                    statusCode !== "Completed" &&
                    statusCode !== "Cancelled" &&
                    new Date(task.dueAt).getTime() < Date.now();

                  return (
                    <tr
                      key={task.id}
                      className="border-ink-950/10 hover:bg-ink-950/5 cursor-pointer border-b transition-colors last:border-b-0"
                      onClick={() => router.push(`/tasks/${task.id}`)}
                    >
                      <td className="px-4 py-3 font-medium">{task.title}</td>
                      <td className="px-4 py-3">{employeeDeptText}</td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <Badge tone={statusTone(statusCode)}>
                            {statusCode}
                          </Badge>
                          {isMissed ? (
                            <Badge tone="danger">
                              Просрочено (Missed)
                            </Badge>
                          ) : null}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <Badge tone={statusTone(priorityCode)}>
                          {priorityCode}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        {formatDateTime(task.scheduledStartAt, locale)}
                      </td>
                      <td className="px-4 py-3">
                        {formatDateTime(task.dueAt, locale)}
                      </td>
                      {isManagerUser && scope === "Past" ? (
                        <td className="px-4 py-3 text-end" onClick={(e) => e.stopPropagation()}>
                          <Button
                            size="sm"
                            variant="secondary"
                            busy={downloadingTaskId === task.id}
                            onClick={async () => {
                              setDownloadingTaskId(task.id);
                              try {
                                const detail = await apiRequest<Schemas["TaskDto"]>(`/tasks/${task.id}`);
                                await generateAndSavePdf(detail, locale);
                              } catch (err) {
                                console.error("PDF generation failed:", err);
                              } finally {
                                setDownloadingTaskId(null);
                              }
                            }}
                            className="inline-flex items-center gap-1 text-xs"
                          >
                            <Download className="size-3.5 text-indigo-600" /> PDF
                          </Button>
                        </td>
                      ) : null}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Mobile Card List View */}
          <div className="grid gap-2.5 p-1 md:hidden">
            {tasks.map((task) => {
              const statusCode = enumCode("taskStatus", task.status);
              const priorityCode = enumCode("priority", task.priority);
              const empName = task.assigneeName ?? "—";
              const dept = references.departments.find(
                (d) => d.id === task.departmentId,
              );
              const deptName = task.departmentName ?? dept?.name ?? "—";
              const employeeDeptText = `${empName} / ${deptName}`;
              const isMissed =
                statusCode !== "Completed" &&
                statusCode !== "Cancelled" &&
                new Date(task.dueAt).getTime() < Date.now();

              const statusBorderColor =
                {
                  Pending: "border-s-amber-500",
                  InProgress: "border-s-blue-500",
                  SubmittedForApproval: "border-s-purple-500",
                  Completed: "border-s-emerald-500",
                  Cancelled: "border-s-red-500",
                }[statusCode] ?? "border-s-brand-500";

              return (
                <div
                  key={task.id}
                  onClick={() => router.push(`/tasks/${task.id}`)}
                  className={`border-ink-950/10 hover:bg-ink-950/5 border-s-4 ${statusBorderColor} flex cursor-pointer items-center justify-between gap-3 rounded-xl border bg-surface p-3.5 shadow-xs transition-all active:scale-[0.99]`}
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-bold text-ink-900">
                      {task.title}
                    </p>
                    <p className="mt-1 truncate text-xs font-medium text-ink-600">
                      {employeeDeptText}
                    </p>
                    <div className="mt-2 flex flex-wrap items-center gap-1.5">
                      <Badge tone={statusTone(statusCode)}>
                        {statusCode}
                      </Badge>
                      {isMissed ? (
                        <Badge tone="danger">
                          Просрочено (Missed)
                        </Badge>
                      ) : null}
                      <Badge tone={statusTone(priorityCode)}>
                        {priorityCode}
                      </Badge>
                    </div>
                  </div>

                  <div className="shrink-0 text-end text-xs font-medium text-ink-500">
                    <p className="font-semibold text-ink-700">
                      {formatDateTime(task.scheduledStartAt, locale)}
                    </p>
                    <p className="mt-1 text-[11px] text-ink-400">
                      → {formatDateTime(task.dueAt, locale)}
                    </p>
                    {isManagerUser && scope === "Past" ? (
                      <div className="mt-2 flex justify-end" onClick={(e) => e.stopPropagation()}>
                        <Button
                          size="sm"
                          variant="secondary"
                          busy={downloadingTaskId === task.id}
                          onClick={async () => {
                            setDownloadingTaskId(task.id);
                            try {
                              const detail = await apiRequest<Schemas["TaskDto"]>(`/tasks/${task.id}`);
                              await generateAndSavePdf(detail, locale);
                            } catch (err) {
                              console.error("PDF generation failed:", err);
                            } finally {
                              setDownloadingTaskId(null);
                            }
                          }}
                          className="inline-flex items-center gap-1 text-[11px] py-1 px-2"
                        >
                          <Download className="size-3 text-indigo-600" /> PDF
                        </Button>
                      </div>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}
    </Card>
  );
}

export function TaskListPage({ scope }: { scope: "Upcoming" | "Past" }) {
  const tCommon = useTranslations("Common");
  const locale = useLocale();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { identity } = useAuth();
  const references = useReferenceData();

  const page = Math.max(1, Number(searchParams.get("page") ?? 1));
  const pageSize = 20;
  const search = searchParams.get("search") ?? "";
  const assigneeUserId = searchParams.get("assigneeUserId") ?? "";
  const departmentId = searchParams.get("departmentId") ?? "";
  const status = searchParams.get("status") ?? "";
  const priority = searchParams.get("priority") ?? "";
  const from = searchParams.get("from") ?? "";
  const to = searchParams.get("to") ?? "";

  const apiQueryParams = compactParams({
    page,
    pageSize,
    scope,
    search: search.trim() || undefined,
    assigneeUserId: assigneeUserId || undefined,
    departmentId: departmentId || undefined,
    status: status || undefined,
    priority: priority || undefined,
    from: from ? new Date(from).toISOString() : undefined,
    to: to ? new Date(to).toISOString() : undefined,
  });

  const query = useQuery({
    queryKey: [
      "tasks",
      scope,
      page,
      search,
      assigneeUserId,
      departmentId,
      status,
      priority,
      from,
      to,
    ],
    queryFn: () =>
      apiRequest<PagedResponse<Schemas["TaskDto"]>>(`/tasks${apiQueryParams}`),
  });

  const groupedByDate = useMemo(() => {
    const map = new Map<string, Schemas["TaskDto"][]>();
    for (const task of query.data?.items ?? []) {
      const d = new Date(task.scheduledStartAt);
      const day = String(d.getDate()).padStart(2, "0");
      const month = String(d.getMonth() + 1).padStart(2, "0");
      const year = d.getFullYear();
      const dateKey = `${day}.${month}.${year}`;
      if (!map.has(dateKey)) {
        map.set(dateKey, []);
      }
      map.get(dateKey)!.push(task);
    }
    return Array.from(map.entries());
  }, [query.data?.items]);

  function updateFilter(key: string, value: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (value) {
      params.set(key, value);
    } else {
      params.delete(key);
    }
    params.set("page", "1");
    router.replace(`${pathname}?${params.toString()}`);
  }

  function clearFilters() {
    router.replace(pathname);
  }

  function goToPage(nextPage: number) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("page", String(nextPage));
    router.replace(`${pathname}?${params.toString()}`);
  }

  const userRole = tenantRole(identity);
  const isEmployeeOrSupervisor =
    userRole === "Employee" || userRole === "Supervisor";
  const canCreate = userRole === "Manager" || userRole === "Supervisor";

  const pageTitle =
    scope === "Upcoming"
      ? tCommon("upcomingTasksTitle")
      : tCommon("pastTasksTitle");
  const pageDescription =
    scope === "Upcoming"
      ? tCommon("upcomingTasksDescription")
      : tCommon("pastTasksDescription");

  const totalPages = Math.max(
    1,
    Math.ceil(Number(query.data?.totalCount ?? 0) / pageSize),
  );
  const hasFilters = isEmployeeOrSupervisor
    ? Boolean(from || to)
    : Boolean(assigneeUserId || departmentId || status || priority || from || to);

  return (
    <>
      <PageHeader
        title={pageTitle}
        description={pageDescription}
        actions={
          canCreate ? (
            <Button>
              <Link href="/tasks/new" className="inline-flex items-center gap-2">
                <Plus className="size-4" /> {tCommon("create")}
              </Link>
            </Button>
          ) : null
        }
      />
      <Card className="mb-4 p-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {!isEmployeeOrSupervisor ? (
            <>
              <Field label={tCommon("employee")}>
                <Select
                  value={assigneeUserId}
                  onChange={(e) => updateFilter("assigneeUserId", e.target.value)}
                >
                  <option value="">{tCommon("all")}</option>
                  {references.members.map((member) => (
                    <option key={member.membershipId} value={member.userId}>
                      {member.fullName}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={tCommon("department")}>
                <Select
                  value={departmentId}
                  onChange={(e) => updateFilter("departmentId", e.target.value)}
                >
                  <option value="">{tCommon("all")}</option>
                  {references.departments.map((dept) => (
                    <option key={dept.id} value={dept.id}>
                      {dept.name}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={tCommon("status")}>
                <Select
                  value={status}
                  onChange={(e) => updateFilter("status", e.target.value)}
                >
                  <option value="">{tCommon("all")}</option>
                  {enumCodes.taskStatus.map((code) => (
                    <option key={code} value={code}>
                      {code}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label={tCommon("priority")}>
                <Select
                  value={priority}
                  onChange={(e) => updateFilter("priority", e.target.value)}
                >
                  <option value="">{tCommon("all")}</option>
                  {enumCodes.priority.map((code) => (
                    <option key={code} value={code}>
                      {code}
                    </option>
                  ))}
                </Select>
              </Field>
            </>
          ) : null}
          <Field label={tCommon("startDateFrom")}>
            <Input
              type="date"
              value={from}
              onChange={(e) => updateFilter("from", e.target.value)}
            />
          </Field>
          <Field label={tCommon("startDateTo")}>
            <Input
              type="date"
              value={to}
              onChange={(e) => updateFilter("to", e.target.value)}
            />
          </Field>
          {hasFilters ? (
            <div className="flex items-end">
              <Button
                variant="secondary"
                className="w-full"
                onClick={clearFilters}
              >
                {tCommon("clearFilters")}
              </Button>
            </div>
          ) : null}
        </div>
      </Card>

      {query.isLoading ? (
        <div className="grid gap-3">
          <Skeleton className="h-12" />
          <Skeleton className="h-12" />
          <Skeleton className="h-12" />
        </div>
      ) : query.error ? (
        <Alert tone="danger">
          <p>{errorMessage(query.error)}</p>
          <Button
            className="mt-3"
            size="sm"
            variant="secondary"
            onClick={() => void query.refetch()}
          >
            {tCommon("retry")}
          </Button>
        </Alert>
      ) : !query.data?.items.length ? (
        <EmptyState title={tCommon("noResults")} />
      ) : (
        <>
          {groupedByDate.map(([dateKey, tasks], index) => (
            <TaskDateGroup
              key={dateKey}
              dateLabel={dateKey}
              tasks={tasks}
              defaultOpen={index < 2}
              references={references}
              locale={locale}
              router={router}
              tCommon={tCommon}
              scope={scope}
              isManagerUser={userRole === "Manager"}
            />
          ))}

          {totalPages > 1 ? (
            <Card className="flex items-center justify-between p-4">
              <span className="text-ink-600 text-sm">
                {tCommon("page", { page })} / {totalPages}
              </span>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={page <= 1}
                  onClick={() => goToPage(page - 1)}
                >
                  {tCommon("previous")}
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => goToPage(page + 1)}
                >
                  {tCommon("next")}
                </Button>
              </div>
            </Card>
          ) : null}
        </>
      )}
    </>
  );
}

export function UpcomingTasksPage() {
  return <TaskListPage scope="Upcoming" />;
}

export function PastTasksPage() {
  return <TaskListPage scope="Past" />;
}

export function TaskTemplateDetail({ id }: { id: string }) {
  const queryClient = useQueryClient();
  const router = useRouter();
  const toast = useToast();
  const query = useQuery({
    queryKey: queryKeys.taskTemplates.detail(id),
    queryFn: () =>
      apiRequest<Schemas["TaskTemplateDto"]>(`/task-templates/${id}`),
  });
  const mutation = useMutation({
    mutationFn: (action: "clone" | "activate" | "deactivate" | "delete") =>
      apiRequest(
        action === "delete"
          ? `/task-templates/${id}`
          : `/task-templates/${id}/${action}`,
        { method: action === "delete" ? "DELETE" : "POST" },
      ),
    onSuccess: (_, action) => {
      void queryClient.invalidateQueries({ queryKey: ["task-templates"] });
      toast.push(`Template ${action} completed.`);
      if (action === "delete") router.push("/task-templates");
      else void query.refetch();
    },
  });
  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error || !query.data)
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  return (
    <>
      <PageHeader
        title={query.data.title}
        description="Historical task snapshots are isolated from future template edits."
        actions={
          <>
            <Button variant="secondary">
              <Link href={`/task-templates/${id}/edit`}>Edit</Link>
            </Button>
            <Button>
              <Link href={`/tasks/new?templateId=${id}`}>Use template</Link>
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
      {mutation.error ? (
        <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
      ) : null}
      <DetailGrid
        data={query.data as unknown as Record<string, unknown>}
        omit={["items", "title"]}
      />
      <Card className="mt-5">
        <h2 className="mb-4 text-lg font-black">Checklist definition</h2>
        <ol className="grid gap-3">
          {query.data.items.map((item) => (
            <li
              key={item.id}
              className="border-ink-950/10 flex items-start gap-3 rounded-xl border p-3"
            >
              <span className="bg-brand-100 text-brand-700 grid size-7 shrink-0 place-items-center rounded-lg text-xs font-black">
                {Number(item.sortOrder) + 1}
              </span>
              <div>
                <p className="font-bold">{item.title}</p>
                <p className="text-ink-600 text-sm">{item.description}</p>
              </div>
            </li>
          ))}
        </ol>
      </Card>
      <div className="mt-5 flex flex-wrap gap-2">
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
