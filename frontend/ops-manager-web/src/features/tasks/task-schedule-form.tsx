"use client";

import { useEffect } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm, useWatch } from "react-hook-form";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Button,
  Card,
  Field,
  Input,
  Select,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCodes, enumValue } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { scheduleSchema, type ScheduleValues } from "@/lib/forms/validation";
import { queryKeys } from "@/lib/query/query-keys";

import { useReferenceData } from "../shared/reference-data";
import {
  type AssignmentMode,
  TaskAssignmentFields,
} from "./task-assignment-fields";

const WEEKDAY_LABELS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function TaskScheduleForm({ id }: { id?: string }) {
  const references = useReferenceData();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: id
      ? queryKeys.taskSchedules.detail(id)
      : ["task-schedule", "new"],
    queryFn: () =>
      apiRequest<Schemas["TaskScheduleDto"]>(`/task-schedules/${id}`),
    enabled: Boolean(id),
  });
  const form = useForm<ScheduleValues>({
    resolver: zodResolver(scheduleSchema),
    defaultValues: {
      taskTemplateId: "",
      branchId: "",
      departmentId: "",
      assignmentMode: "SingleUser",
      assigneeUserIds: [],
      recurrenceType: "Daily",
      weekdays: [],
      monthDays: [],
      includeLastDayOfMonth: false,
      specificDates: [],
      recurrenceStartDate: new Date().toISOString().slice(0, 10),
      recurrenceEndDate: "",
      executionStartTime: "09:00",
      executionDueTime: "10:00",
      executionDueDayOffset: 0,
      isActive: true,
    },
  });
  const recurrence = useWatch({
    control: form.control,
    name: "recurrenceType",
  });
  const weekdays = useWatch({ control: form.control, name: "weekdays" });
  const monthDays = useWatch({ control: form.control, name: "monthDays" });
  const includeLastDay = useWatch({
    control: form.control,
    name: "includeLastDayOfMonth",
  });
  const specificDates = useWatch({
    control: form.control,
    name: "specificDates",
  });
  const [assignmentDepartmentId, assignmentMode, assignmentUserIds] = useWatch({
    control: form.control,
    name: ["departmentId", "assignmentMode", "assigneeUserIds"],
  });
  useEffect(() => {
    if (!query.data) return;
    form.reset({
      taskTemplateId: query.data.taskTemplateId,
      branchId: query.data.branchId,
      departmentId: query.data.departmentId,
      assignmentMode:
        (enumCodes.taskAssignmentMode[
          Number(query.data.assignmentMode)
        ] as AssignmentMode) ?? "SingleUser",
      assigneeUserIds: query.data.assigneeUserIds,
      recurrenceType:
        (enumCodes.recurrence[Number(query.data.recurrenceType)] as
          | "Daily"
          | "Weekly"
          | "Monthly"
          | "SpecificDates") ?? "Daily",
      weekdays: (query.data.weekdays ?? []).map(Number),
      monthDays: (query.data.monthDays ?? []).map(Number),
      includeLastDayOfMonth: query.data.includeLastDayOfMonth ?? false,
      specificDates: query.data.specificDates ?? [],
      recurrenceStartDate: query.data.recurrenceStartDate,
      recurrenceEndDate: query.data.recurrenceEndDate ?? "",
      executionStartTime: query.data.executionStartTime,
      executionDueTime: query.data.executionDueTime,
      executionDueDayOffset: Number(query.data.executionDueDayOffset),
      isActive: query.data.isActive,
    });
  }, [form, query.data]);
  const mutation = useMutation({
    mutationFn: (values: ScheduleValues) =>
      apiRequest(id ? `/task-schedules/${id}` : "/task-schedules", {
        method: id ? "PATCH" : "POST",
        body: {
          taskTemplateId: values.taskTemplateId,
          branchId: values.branchId,
          departmentId: values.departmentId,
          assignment: {
            mode: enumValue("taskAssignmentMode", values.assignmentMode),
            userIds: values.assigneeUserIds,
          },
          recurrenceType: enumValue("recurrence", values.recurrenceType),
          weekdays:
            values.recurrenceType === "Weekly" ? values.weekdays : [],
          monthDays:
            values.recurrenceType === "Monthly" ? values.monthDays : [],
          includeLastDayOfMonth:
            values.recurrenceType === "Monthly"
              ? values.includeLastDayOfMonth
              : false,
          specificDates:
            values.recurrenceType === "SpecificDates"
              ? values.specificDates
              : [],
          recurrenceStartDate: values.recurrenceStartDate,
          recurrenceEndDate: values.recurrenceEndDate || null,
          executionStartTime: values.executionStartTime,
          executionDueTime: values.executionDueTime,
          executionDueDayOffset: values.executionDueDayOffset,
          isActive: values.isActive,
        },
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["task-schedules"] });
      void queryClient.invalidateQueries({ queryKey: ["tasks"] });
      toast.push(id ? "Schedule updated." : "Schedule created.");
      router.push("/task-schedules");
    },
  });

  const isReadOnly = Boolean(id);

  const statusMutation = useMutation({
    mutationFn: (activate: boolean) =>
      apiRequest(`/task-schedules/${id}/${activate ? "activate" : "deactivate"}`, {
        method: "POST",
      }),
    onSuccess: (_, activate) => {
      void queryClient.invalidateQueries({ queryKey: ["task-schedules"] });
      void queryClient.invalidateQueries({ queryKey: ["tasks"] });
      toast.push(activate ? "Schedule activated." : "Schedule deactivated.");
      form.setValue("isActive", activate);
    },
  });

  const toggleWeekday = (day: number) => {
    if (isReadOnly) return;
    const current = form.getValues("weekdays");
    const next = current.includes(day)
      ? current.filter((d) => d !== day)
      : [...current, day].sort();
    form.setValue("weekdays", next, { shouldValidate: true });
  };

  const toggleMonthDay = (day: number) => {
    if (isReadOnly) return;
    const current = form.getValues("monthDays");
    const next = current.includes(day)
      ? current.filter((d) => d !== day)
      : [...current, day].sort((a, b) => a - b);
    form.setValue("monthDays", next, { shouldValidate: true });
  };

  const addSpecificDate = (date: string) => {
    if (!date || isReadOnly) return;
    const current = form.getValues("specificDates");
    if (!current.includes(date)) {
      form.setValue("specificDates", [...current, date].sort(), {
        shouldValidate: true,
      });
    }
  };

  const removeSpecificDate = (date: string) => {
    if (isReadOnly) return;
    const current = form.getValues("specificDates");
    form.setValue(
      "specificDates",
      current.filter((d) => d !== date),
      { shouldValidate: true },
    );
  };

  const summary =
    recurrence === "Daily"
      ? "Every day"
      : recurrence === "Weekly"
        ? `Weekly on ${weekdays.map((d) => WEEKDAY_LABELS[d]).join(", ") || "\u2026"}`
        : recurrence === "Monthly"
          ? `Monthly on day${monthDays.length > 1 ? "s" : ""} ${monthDays.join(", ") || "\u2026"}${includeLastDay ? " + last day" : ""}`
          : `On ${specificDates.length} specific date(s)`;

  return (
    <>
      <PageHeader
        title={id ? "Task schedule details" : "Create task schedule"}
        description={summary}
      />
      <Card className="max-w-4xl">
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={form.handleSubmit(
            (values) => {
              if (!isReadOnly) {
                mutation.mutate(values);
              }
            },
            (errors) => {
              const firstErrorKey = Object.keys(errors)[0];
              const firstErrorMessage =
                errors[firstErrorKey as keyof typeof errors]?.message;
              if (firstErrorMessage) {
                toast.push(String(firstErrorMessage));
              }
            },
          )}
        >
          {mutation.error ? (
            <div className="md:col-span-2">
              <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
            </div>
          ) : null}
          {statusMutation.error ? (
            <div className="md:col-span-2">
              <Alert tone="danger">{errorMessage(statusMutation.error)}</Alert>
            </div>
          ) : null}
          <Field label="Task template" required>
            <Select disabled={isReadOnly} {...form.register("taskTemplateId")}>
              <option value="">Select template</option>
              {references.taskTemplates.map((template) => (
                <option key={template.id} value={template.id}>
                  {template.title}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Branch" required>
            <Select disabled={isReadOnly} {...form.register("branchId")}>
              <option value="">Select branch</option>
              {references.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Department" required>
            <Select disabled={isReadOnly} {...form.register("departmentId")}>
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
            disabled={isReadOnly}
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
          <Field label="Recurrence type" required>
            <Select disabled={isReadOnly} {...form.register("recurrenceType")}>
              <option value="Daily">Daily</option>
              <option value="Weekly">Weekly</option>
              <option value="Monthly">Monthly</option>
              <option value="SpecificDates">Specific dates</option>
            </Select>
          </Field>
          {recurrence === "Weekly" ? (
            <div className="md:col-span-2">
              <Field
                label="Weekdays"
                error={form.formState.errors.weekdays?.message}
                required
              >
                <div className="flex flex-wrap gap-2.5 pt-1">
                  {WEEKDAY_LABELS.map((label, index) => (
                    <button
                      key={index}
                      type="button"
                      disabled={isReadOnly}
                      className={`rounded-xl border px-4 py-2 text-sm font-bold transition-all shadow-xs ${
                        weekdays.includes(index)
                          ? "border-indigo-600 bg-indigo-600 text-white shadow-md scale-105"
                          : "border-slate-200 bg-slate-100 text-slate-700 hover:bg-slate-200 hover:text-slate-900"
                      } ${isReadOnly ? "opacity-75 cursor-not-allowed" : ""}`}
                      onClick={() => toggleWeekday(index)}
                      aria-pressed={weekdays.includes(index)}
                    >
                      {label}
                    </button>
                  ))}
                </div>
              </Field>
            </div>
          ) : null}
          {recurrence === "Monthly" ? (
            <>
              <div className="md:col-span-2">
                <Field
                  label="Days of the month"
                  error={form.formState.errors.monthDays?.message}
                  required={!includeLastDay}
                >
                  <div className="grid grid-cols-7 gap-1.5 sm:grid-cols-10 pt-1">
                    {Array.from({ length: 31 }, (_, i) => i + 1).map((day) => (
                      <button
                        key={day}
                        type="button"
                        disabled={isReadOnly}
                        className={`rounded-lg border px-3 py-1.5 text-xs font-bold transition-all ${
                          monthDays.includes(day)
                            ? "border-indigo-600 bg-indigo-600 text-white shadow-md scale-105 ring-2 ring-indigo-200"
                            : "border-slate-200 bg-slate-100 text-slate-700 hover:bg-slate-200 hover:text-slate-900"
                        } ${isReadOnly ? "opacity-75 cursor-not-allowed" : ""}`}
                        onClick={() => toggleMonthDay(day)}
                        aria-pressed={monthDays.includes(day)}
                      >
                        {day}
                      </button>
                    ))}
                  </div>
                </Field>
              </div>
              <div className="md:col-span-2 bg-indigo-50/60 border border-indigo-100 p-3 rounded-xl">
                <label className="flex items-center gap-2.5 text-sm font-bold text-indigo-950 cursor-pointer">
                  <input
                    type="checkbox"
                    disabled={isReadOnly}
                    className="size-4 rounded text-indigo-600 focus:ring-indigo-500"
                    checked={includeLastDay}
                    onChange={(e) =>
                      form.setValue(
                        "includeLastDayOfMonth",
                        e.target.checked,
                        { shouldValidate: true },
                      )
                    }
                  />
                  Include last day of every month
                </label>
              </div>
            </>
          ) : null}
          {recurrence === "SpecificDates" ? (
            <div className="md:col-span-2">
              <Field
                label="Specific dates"
                error={form.formState.errors.specificDates?.message}
                required
              >
              {!isReadOnly ? (
                <div className="flex items-center gap-2">
                  <Input
                    type="date"
                    id="specific-date-picker"
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        e.preventDefault();
                        addSpecificDate(
                          (e.target as HTMLInputElement).value,
                        );
                      }
                    }}
                  />
                  <Button
                    type="button"
                    onClick={() => {
                      const input = document.getElementById(
                        "specific-date-picker",
                      ) as HTMLInputElement;
                      addSpecificDate(input.value);
                    }}
                  >
                    Add
                  </Button>
                </div>
              ) : null}
              {specificDates.length > 0 ? (
                <div className="mt-2 flex flex-wrap gap-2">
                  {specificDates.map((date) => (
                    <span
                      key={date}
                      className="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 bg-indigo-50 px-3 py-1 text-sm font-bold text-indigo-900 shadow-2xs"
                    >
                      {date}
                      {!isReadOnly ? (
                        <button
                          type="button"
                          className="ml-1 font-bold text-rose-500 hover:text-rose-700 transition-colors"
                          onClick={() => removeSpecificDate(date)}
                          aria-label={`Remove ${date}`}
                        >
                          &times;
                        </button>
                      ) : null}
                    </span>
                  ))}
                </div>
              ) : null}
            </Field>
          </div>
          ) : null}
          <Field label="Recurrence start date" required>
            <Input
              type="date"
              disabled={isReadOnly}
              {...form.register("recurrenceStartDate")}
            />
          </Field>
          <Field label="Recurrence end date">
            <Input type="date" disabled={isReadOnly} {...form.register("recurrenceEndDate")} />
          </Field>
          <Field label="Execution start time" required>
            <Input
              type="time"
              disabled={isReadOnly}
              {...form.register("executionStartTime")}
            />
          </Field>
          <Field label="Execution due time" required>
            <Input type="time" disabled={isReadOnly} {...form.register("executionDueTime")} />
          </Field>
          <Field label="Due day" required>
            <Select
              disabled={isReadOnly}
              {...form.register("executionDueDayOffset", {
                valueAsNumber: true,
              })}
            >
              <option value={0}>Same day</option>
              <option value={1}>Next day</option>
            </Select>
          </Field>
          <div className="md:col-span-2 pt-2">
            <div className="flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-center gap-3">
                <span className="text-sm font-bold text-slate-700">Status:</span>
                <span
                  className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-bold ${
                    form.watch("isActive")
                      ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
                      : "bg-rose-100 text-rose-800 border border-rose-300"
                  }`}
                >
                  {form.watch("isActive") ? "Active" : "Inactive"}
                </span>
              </div>
              {id ? (
                <Button
                  type="button"
                  variant={form.watch("isActive") ? "danger" : "primary"}
                  busy={statusMutation.isPending}
                  onClick={() => statusMutation.mutate(!form.watch("isActive"))}
                >
                  {form.watch("isActive") ? "Deactivate schedule" : "Activate schedule"}
                </Button>
              ) : (
                <label className="flex items-center gap-2 text-sm font-semibold">
                  <input type="checkbox" {...form.register("isActive")} /> Active
                </label>
              )}
            </div>
          </div>
          {!id ? (
            <div className="md:col-span-2 pt-2">
              <Button type="submit" busy={mutation.isPending}>
                Create schedule
              </Button>
            </div>
          ) : null}
        </form>
      </Card>
    </>
  );
}
