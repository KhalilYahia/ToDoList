"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useForm, useWatch } from "react-hook-form";
import { useLocale } from "next-intl";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Badge,
  Button,
  Card,
  Field,
  Input,
  Select,
  Skeleton,
  Textarea,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { Link, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode, enumCodes, enumValue, statusTone } from "@/lib/api/enums";
import { ApiError, errorMessage } from "@/lib/api/errors";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  paymentSchema,
  planSchema,
  type PaymentValues,
  type PlanValues,
} from "@/lib/forms/validation";
import { isPlatformAdministrator } from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";
import {
  formatDateTime,
  localInputToIso,
  toLocalInputValue,
} from "@/lib/utils";

import { DetailGrid } from "../shared/detail-grid";

function usePlatformReferences() {
  const [organizations, plans] = useQueries({
    queries: [
      {
        queryKey: queryKeys.platform.organizations({ pageSize: 200 }),
        queryFn: () =>
          apiRequest<PagedResponse<Schemas["PlatformOrganizationDto"]>>(
            "/platform/organizations?page=1&pageSize=200",
            { realm: "platform" },
          ),
      },
      {
        queryKey: queryKeys.platform.plans({ pageSize: 200 }),
        queryFn: () =>
          apiRequest<PagedResponse<Schemas["SubscriptionPlanDto"]>>(
            "/platform/subscription-plans?page=1&pageSize=200",
            { realm: "platform" },
          ),
      },
    ],
  });
  return {
    organizations: organizations.data?.items ?? [],
    plans: plans.data?.items ?? [],
    isLoading: organizations.isLoading || plans.isLoading,
  };
}

export function PlatformOrganizationDetail({ id }: { id: string }) {
  const locale = useLocale();
  const { identity } = useAuth();
  const references = usePlatformReferences();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [planId, setPlanId] = useState("");
  const [billingMode, setBillingMode] = useState("MonthlyBilling");
  const [startsAt, setStartsAt] = useState(
    toLocalInputValue(new Date().toISOString()),
  );
  const [endsAt, setEndsAt] = useState("");
  const [reason, setReason] = useState("");
  const [complimentary, setComplimentary] = useState(false);
  const organizationQuery = useQuery({
    queryKey: queryKeys.platform.organization(id),
    queryFn: () =>
      apiRequest<Schemas["PlatformOrganizationDto"]>(
        `/platform/organizations/${id}`,
        { realm: "platform" },
      ),
  });
  const subscriptionQuery = useQuery({
    queryKey: [...queryKeys.platform.organization(id), "subscription"],
    queryFn: () =>
      apiRequest<Schemas["OrganizationSubscriptionDto"]>(
        `/platform/organizations/${id}/subscription`,
        { realm: "platform" },
      ),
    retry: false,
  });
  const actionMutation = useMutation({
    mutationFn: ({
      action,
      body,
    }: {
      action: string;
      body: Record<string, unknown>;
    }) =>
      apiRequest<Schemas["OrganizationSubscriptionDto"]>(
        `/platform/organizations/${id}/subscription/${action}`,
        { method: "POST", body, realm: "platform" },
      ),
    onSuccess: (subscription) => {
      queryClient.setQueryData(
        [...queryKeys.platform.organization(id), "subscription"],
        subscription,
      );
      void organizationQuery.refetch();
      void queryClient.invalidateQueries({ queryKey: ["platform"] });
      setReason("");
      toast.push("Subscription updated.");
    },
  });
  const administrator = isPlatformAdministrator(identity);

  function mutate(
    action: string,
    body: Record<string, unknown>,
    destructive = false,
  ) {
    if (
      destructive &&
      !window.confirm(
        `Confirm ${action.replace("-", " ")} for this organization?`,
      )
    ) {
      return;
    }
    actionMutation.mutate({ action, body });
  }

  if (organizationQuery.isLoading) return <Skeleton className="h-[30rem]" />;
  if (organizationQuery.error || !organizationQuery.data) {
    return <Alert tone="danger">{errorMessage(organizationQuery.error)}</Alert>;
  }
  const organization = organizationQuery.data;
  const subscription = subscriptionQuery.data;
  const subscriptionMissing =
    subscriptionQuery.error instanceof ApiError &&
    subscriptionQuery.error.status === 404;

  return (
    <>
      <PageHeader
        title={organization.name}
        description={organization.legalName ?? "Platform organization"}
        actions={
          <>
            <Badge
              tone={statusTone(
                enumCode("organizationStatus", organization.status),
              )}
            >
              {enumCode("organizationStatus", organization.status)}
            </Badge>
            {administrator ? (
              <Button variant="secondary">
                <Link href={`/platform/organizations/${id}/branches`}>
                  Manage branches
                </Link>
              </Button>
            ) : null}
            {organization.subscriptionStatus !== null ? (
              <Badge
                tone={statusTone(
                  enumCode(
                    "subscriptionStatus",
                    organization.subscriptionStatus,
                  ),
                )}
              >
                {enumCode(
                  "subscriptionStatus",
                  organization.subscriptionStatus,
                )}
              </Badge>
            ) : null}
          </>
        }
      />
      {!administrator ? (
        <Alert tone="info">
          Support users have read-only platform access. Subscription actions are
          available only to administrators.
        </Alert>
      ) : null}
      <div className="mt-5 grid gap-5 xl:grid-cols-[1fr_24rem]">
        <div className="grid content-start gap-5">
          <DetailGrid
            data={organization as unknown as Record<string, unknown>}
          />
          {subscriptionQuery.isLoading ? (
            <Skeleton className="h-64" />
          ) : subscription ? (
            <DetailGrid
              data={{
                id: subscription.id,
                planId: subscription.planId,
                status: enumCode("subscriptionStatus", subscription.status),
                billingMode: enumCode("billingMode", subscription.billingMode),
                startsAt: subscription.startsAt,
                endsAt: subscription.endsAt,
                trialEndsAt: subscription.trialEndsAt,
                gracePeriodEndsAt: subscription.gracePeriodEndsAt,
                suspensionReason: subscription.suspensionReason,
                notes: subscription.notes,
              }}
            />
          ) : subscriptionMissing ? (
            <Alert tone="warning" title="No subscription record">
              Activate a plan to create the organization subscription.
            </Alert>
          ) : (
            <Alert tone="danger">{errorMessage(subscriptionQuery.error)}</Alert>
          )}
          <Alert tone="info">
            User/branch usage, subscription history, and platform audit entries
            are not exposed by the current API and are therefore not estimated
            on this page.
          </Alert>
        </div>
        <div className="grid content-start gap-5">
          <Card>
            <h2 className="mb-3 text-lg font-black">Activate or change plan</h2>
            <div className="grid gap-3">
              <Field label="Plan">
                <Select
                  value={planId}
                  disabled={!administrator}
                  onChange={(event) => setPlanId(event.target.value)}
                >
                  <option value="">Select active plan</option>
                  {references.plans
                    .filter((plan) => plan.isActive)
                    .map((plan) => (
                      <option key={plan.id} value={plan.id}>
                        {plan.name}
                      </option>
                    ))}
                </Select>
              </Field>
              <Field label="Billing mode">
                <Select
                  value={billingMode}
                  disabled={!administrator}
                  onChange={(event) => setBillingMode(event.target.value)}
                >
                  {enumCodes.billingMode.map((mode) => (
                    <option key={mode}>{mode}</option>
                  ))}
                </Select>
              </Field>
              <Field label="Starts at">
                <Input
                  type="datetime-local"
                  value={startsAt}
                  disabled={!administrator}
                  onChange={(event) => setStartsAt(event.target.value)}
                />
              </Field>
              <Field label="Ends at">
                <Input
                  type="datetime-local"
                  value={endsAt}
                  disabled={!administrator}
                  onChange={(event) => setEndsAt(event.target.value)}
                />
              </Field>
              <label className="flex items-center gap-2 text-sm font-semibold">
                <input
                  type="checkbox"
                  checked={complimentary}
                  disabled={!administrator}
                  onChange={(event) => setComplimentary(event.target.checked)}
                />
                Complimentary access
              </label>
              <Button
                disabled={!administrator || !planId || !startsAt}
                busy={actionMutation.isPending}
                onClick={() =>
                  mutate("activate", {
                    planId,
                    billingMode: enumValue("billingMode", billingMode),
                    startsAt: localInputToIso(startsAt),
                    endsAt: endsAt ? localInputToIso(endsAt) : null,
                    complimentary,
                    reason: reason || null,
                  })
                }
              >
                Activate subscription
              </Button>
              {subscription ? (
                <Button
                  variant="secondary"
                  disabled={!administrator || !planId}
                  onClick={() =>
                    mutate("change-plan", { planId, reason: reason || null })
                  }
                >
                  Change plan
                </Button>
              ) : null}
            </div>
          </Card>
          {subscription ? (
            <Card>
              <h2 className="mb-3 text-lg font-black">
                Subscription lifecycle
              </h2>
              <div className="grid gap-3">
                <Field label="Reason">
                  <Textarea
                    className="min-h-20"
                    value={reason}
                    disabled={!administrator}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </Field>
                <Button
                  variant="secondary"
                  disabled={!administrator || !endsAt}
                  onClick={() =>
                    mutate("extend", {
                      endsAt: localInputToIso(endsAt),
                      reason: reason || null,
                    })
                  }
                >
                  Extend to selected end date
                </Button>
                <Button
                  variant="secondary"
                  disabled={!administrator}
                  onClick={() =>
                    mutate("reactivate", { reason: reason || null })
                  }
                >
                  Reactivate
                </Button>
                <Button
                  variant="danger"
                  disabled={!administrator || !reason.trim()}
                  onClick={() =>
                    mutate("suspend", { reason: reason.trim() }, true)
                  }
                >
                  Suspend
                </Button>
                <Button
                  variant="danger"
                  disabled={!administrator || !reason.trim()}
                  onClick={() =>
                    mutate("expire", { reason: reason.trim() }, true)
                  }
                >
                  Expire
                </Button>
              </div>
            </Card>
          ) : null}
          {organization.subscriptionEndsAt ? (
            <p className="text-ink-600 text-xs">
              Listed end:{" "}
              {formatDateTime(organization.subscriptionEndsAt, locale)}
            </p>
          ) : null}
        </div>
      </div>
    </>
  );
}

function parseFeatures(value: string): Record<string, string> {
  return Object.fromEntries(
    value
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => {
        const separator = line.indexOf("=");
        return separator < 0
          ? [line, "true"]
          : [line.slice(0, separator).trim(), line.slice(separator + 1).trim()];
      }),
  );
}

function usePlatformPlan(id?: string) {
  return useQuery({
    queryKey: id ? queryKeys.platform.plan(id) : ["platform", "plan", "new"],
    queryFn: async () => {
      const data = await apiRequest<
        PagedResponse<Schemas["SubscriptionPlanDto"]>
      >("/platform/subscription-plans?page=1&pageSize=200", {
        realm: "platform",
      });
      const plan = data.items.find((item) => item.id === id);
      if (!plan) throw new Error("Plan not found.");
      return plan;
    },
    enabled: Boolean(id),
  });
}

export function PlatformPlanForm({ id }: { id?: string }) {
  const { identity } = useAuth();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = usePlatformPlan(id);
  const form = useForm<PlanValues>({
    resolver: zodResolver(planSchema),
    defaultValues: {
      name: "",
      code: "",
      description: "",
      monthlyPrice: "",
      yearlyPrice: "",
      currency: "USD",
      maxUsers: 10,
      maxBranches: 1,
      maxStorageMb: 1024,
      features: "tasks=true\norders=true\ncomplaints=true",
      gracePeriodDays: 7,
      isActive: true,
    },
  });
  useEffect(() => {
    if (!query.data) return;
    form.reset({
      name: query.data.name,
      code: query.data.code,
      description: query.data.description ?? "",
      monthlyPrice:
        query.data.monthlyPrice === null ? "" : String(query.data.monthlyPrice),
      yearlyPrice:
        query.data.yearlyPrice === null ? "" : String(query.data.yearlyPrice),
      currency: query.data.currency,
      maxUsers: Number(query.data.maxUsers),
      maxBranches: Number(query.data.maxBranches),
      maxStorageMb: Number(query.data.maxStorageMb),
      features: Object.entries(query.data.features)
        .map(([key, value]) => `${key}=${value}`)
        .join("\n"),
      gracePeriodDays: Number(query.data.gracePeriodDays),
      isActive: query.data.isActive,
    });
  }, [form, query.data]);
  const mutation = useMutation({
    mutationFn: (values: PlanValues) =>
      apiRequest<Schemas["SubscriptionPlanDto"]>(
        id
          ? `/platform/subscription-plans/${id}`
          : "/platform/subscription-plans",
        {
          method: id ? "PATCH" : "POST",
          realm: "platform",
          body: {
            ...values,
            code: values.code.trim(),
            currency: values.currency.trim().toUpperCase(),
            monthlyPrice: values.monthlyPrice
              ? Number(values.monthlyPrice)
              : null,
            yearlyPrice: values.yearlyPrice ? Number(values.yearlyPrice) : null,
            features: parseFeatures(values.features),
          },
        },
      ),
    onSuccess: (plan) => {
      void queryClient.invalidateQueries({ queryKey: ["platform", "plans"] });
      toast.push(id ? "Plan updated." : "Plan created.");
      router.push(`/platform/plans/${plan.id}`);
    },
  });
  if (id && query.isLoading) return <Skeleton className="h-[34rem]" />;
  if (!isPlatformAdministrator(identity)) {
    return (
      <Alert tone="danger">
        Only platform administrators can create or edit plans.
      </Alert>
    );
  }
  return (
    <>
      <PageHeader
        title={id ? "Edit subscription plan" : "New subscription plan"}
        description="Limits and feature flags are enforced by backend subscription services."
      />
      <form
        className="grid max-w-4xl gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {query.error || mutation.error ? (
          <Alert tone="danger">
            {errorMessage(query.error ?? mutation.error)}
          </Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field label="Name" error={form.formState.errors.name?.message}>
            <Input {...form.register("name")} />
          </Field>
          <Field label="Code" error={form.formState.errors.code?.message}>
            <Input {...form.register("code")} />
          </Field>
          <Field label="Monthly price">
            <Input
              type="number"
              min="0"
              step="0.01"
              {...form.register("monthlyPrice")}
            />
          </Field>
          <Field label="Yearly price">
            <Input
              type="number"
              min="0"
              step="0.01"
              {...form.register("yearlyPrice")}
            />
          </Field>
          <Field
            label="Currency"
            error={form.formState.errors.currency?.message}
          >
            <Input maxLength={3} {...form.register("currency")} />
          </Field>
          <Field label="Grace period days">
            <Input
              type="number"
              min="0"
              {...form.register("gracePeriodDays", { valueAsNumber: true })}
            />
          </Field>
          <Field label="Maximum users">
            <Input
              type="number"
              min="1"
              {...form.register("maxUsers", { valueAsNumber: true })}
            />
          </Field>
          <Field label="Maximum branches">
            <Input
              type="number"
              min="1"
              {...form.register("maxBranches", { valueAsNumber: true })}
            />
          </Field>
          <Field label="Maximum storage (MB)">
            <Input
              type="number"
              min="0"
              {...form.register("maxStorageMb", { valueAsNumber: true })}
            />
          </Field>
          <label className="flex items-center gap-2 self-end pb-3 text-sm font-semibold">
            <input type="checkbox" {...form.register("isActive")} />
            Active plan
          </label>
          <div className="md:col-span-2">
            <Field label="Description">
              <Textarea {...form.register("description")} />
            </Field>
          </div>
          <div className="md:col-span-2">
            <Field
              label="Features"
              hint="One key=value pair per line. A key without a value becomes true."
            >
              <Textarea className="font-mono" {...form.register("features")} />
            </Field>
          </div>
        </Card>
        <div>
          <Button type="submit" busy={mutation.isPending}>
            Save plan
          </Button>
        </div>
      </form>
    </>
  );
}

export function PlatformPlanDetail({ id }: { id: string }) {
  const { identity } = useAuth();
  const query = usePlatformPlan(id);
  const toast = useToast();
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: (action: "activate" | "deactivate") =>
      apiRequest(`/platform/subscription-plans/${id}/${action}`, {
        method: "POST",
        realm: "platform",
      }),
    onSuccess: (_, action) => {
      void query.refetch();
      void queryClient.invalidateQueries({ queryKey: ["platform", "plans"] });
      toast.push(`Plan ${action}d.`);
    },
  });
  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error || !query.data) {
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  }
  const administrator = isPlatformAdministrator(identity);
  return (
    <>
      <PageHeader
        title={query.data.name}
        description={query.data.description ?? query.data.code}
        actions={
          <>
            <Badge tone={query.data.isActive ? "success" : "neutral"}>
              {query.data.isActive ? "Active" : "Inactive"}
            </Badge>
            {administrator ? (
              <Button variant="secondary">
                <Link href={`/platform/plans/${id}/edit`}>Edit</Link>
              </Button>
            ) : null}
          </>
        }
      />
      {mutation.error ? (
        <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
      ) : null}
      <DetailGrid
        data={{
          ...query.data,
          features: Object.entries(query.data.features)
            .map(([key, value]) => `${key}: ${value}`)
            .join(", "),
        }}
      />
      {administrator ? (
        <Card className="mt-5">
          <Button
            variant={query.data.isActive ? "danger" : "primary"}
            busy={mutation.isPending}
            onClick={() =>
              mutation.mutate(query.data?.isActive ? "deactivate" : "activate")
            }
          >
            {query.data.isActive ? "Deactivate plan" : "Activate plan"}
          </Button>
        </Card>
      ) : (
        <Alert tone="info">
          Support users can inspect plans but cannot change them.
        </Alert>
      )}
    </>
  );
}

export function PlatformPaymentForm() {
  const { identity } = useAuth();
  const references = usePlatformReferences();
  const router = useRouter();
  const toast = useToast();
  const queryClient = useQueryClient();
  const today = new Date().toISOString().slice(0, 10);
  const form = useForm<PaymentValues>({
    resolver: zodResolver(paymentSchema),
    defaultValues: {
      organizationId: "",
      amount: 0,
      currency: "USD",
      paymentMethod: "BankTransfer",
      paymentReference: "",
      paidAt: "",
      periodStart: today,
      periodEnd: today,
      receiptFileUrl: "",
      note: "",
      activateSubscription: false,
      activationPlanId: "",
      activationEndsAt: "",
    },
  });
  const activate = useWatch({
    control: form.control,
    name: "activateSubscription",
  });
  const mutation = useMutation({
    mutationFn: (values: PaymentValues) =>
      apiRequest<Schemas["ManualPaymentDto"]>("/platform/manual-payments", {
        method: "POST",
        realm: "platform",
        body: {
          ...values,
          currency: values.currency.toUpperCase(),
          paymentMethod: enumValue("paymentMethod", values.paymentMethod),
          paymentReference: values.paymentReference || null,
          paidAt: values.paidAt ? localInputToIso(values.paidAt) : null,
          receiptFileUrl: values.receiptFileUrl || null,
          note: values.note || null,
          activationPlanId: activate ? values.activationPlanId : null,
          activationEndsAt:
            activate && values.activationEndsAt
              ? localInputToIso(values.activationEndsAt)
              : null,
        },
      }),
    onSuccess: (payment) => {
      void queryClient.invalidateQueries({
        queryKey: ["platform", "payments"],
      });
      toast.push("Manual payment recorded.");
      router.push(`/platform/payments/${payment.id}`);
    },
  });
  if (!isPlatformAdministrator(identity)) {
    return (
      <Alert tone="danger">
        Only platform administrators can record payments.
      </Alert>
    );
  }
  return (
    <>
      <PageHeader
        title="Record manual payment"
        description="This records offline payment metadata; it does not initiate online checkout."
      />
      <form
        className="grid max-w-4xl gap-5"
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
      >
        {mutation.error ? (
          <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
        ) : null}
        <Card className="grid gap-4 md:grid-cols-2">
          <Field label="Organization">
            <Select {...form.register("organizationId")}>
              <option value="">Select organization</option>
              {references.organizations.map((organization) => (
                <option key={organization.id} value={organization.id}>
                  {organization.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Payment method">
            <Select {...form.register("paymentMethod")}>
              {enumCodes.paymentMethod.map((method) => (
                <option key={method}>{method}</option>
              ))}
            </Select>
          </Field>
          <Field label="Amount">
            <Input
              type="number"
              min="0"
              step="0.01"
              {...form.register("amount", { valueAsNumber: true })}
            />
          </Field>
          <Field label="Currency">
            <Input maxLength={3} {...form.register("currency")} />
          </Field>
          <Field label="Payment reference">
            <Input {...form.register("paymentReference")} />
          </Field>
          <Field label="Paid at">
            <Input type="datetime-local" {...form.register("paidAt")} />
          </Field>
          <Field label="Period start">
            <Input type="date" {...form.register("periodStart")} />
          </Field>
          <Field
            label="Period end"
            error={form.formState.errors.periodEnd?.message}
          >
            <Input type="date" {...form.register("periodEnd")} />
          </Field>
          <Field
            label="Receipt file URL"
            hint="Metadata URL only; this API has no platform receipt upload endpoint."
          >
            <Input type="url" {...form.register("receiptFileUrl")} />
          </Field>
          <label className="flex items-center gap-2 self-end pb-3 text-sm font-semibold">
            <input type="checkbox" {...form.register("activateSubscription")} />
            Activate subscription with payment
          </label>
          {activate ? (
            <>
              <Field
                label="Activation plan"
                error={form.formState.errors.activationPlanId?.message}
              >
                <Select {...form.register("activationPlanId")}>
                  <option value="">Select active plan</option>
                  {references.plans
                    .filter((plan) => plan.isActive)
                    .map((plan) => (
                      <option key={plan.id} value={plan.id}>
                        {plan.name}
                      </option>
                    ))}
                </Select>
              </Field>
              <Field label="Activation ends at">
                <Input
                  type="datetime-local"
                  {...form.register("activationEndsAt")}
                />
              </Field>
            </>
          ) : null}
          <div className="md:col-span-2">
            <Field label="Note">
              <Textarea {...form.register("note")} />
            </Field>
          </div>
        </Card>
        <div>
          <Button
            type="submit"
            busy={mutation.isPending || references.isLoading}
          >
            Record payment
          </Button>
        </div>
      </form>
    </>
  );
}

export function PlatformPaymentDetail({ id }: { id: string }) {
  const { identity } = useAuth();
  const toast = useToast();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: queryKeys.platform.payment(id),
    queryFn: () =>
      apiRequest<Schemas["ManualPaymentDto"]>(
        `/platform/manual-payments/${id}`,
        { realm: "platform" },
      ),
  });
  const mutation = useMutation({
    mutationFn: (action: "confirm" | "reject" | "refund") =>
      apiRequest<Schemas["ManualPaymentDto"]>(
        `/platform/manual-payments/${id}/${action}`,
        { method: "POST", realm: "platform" },
      ),
    onSuccess: (payment, action) => {
      queryClient.setQueryData(queryKeys.platform.payment(id), payment);
      void queryClient.invalidateQueries({
        queryKey: ["platform", "payments"],
      });
      toast.push(`Payment ${action} completed.`);
    },
  });
  if (query.isLoading) return <Skeleton className="h-96" />;
  if (query.error || !query.data) {
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  }
  const payment = query.data;
  const status = enumCode("paymentStatus", payment.status);
  const administrator = isPlatformAdministrator(identity);
  function changeStatus(action: "confirm" | "reject" | "refund") {
    if (window.confirm(`Confirm payment ${action}?`)) mutation.mutate(action);
  }
  return (
    <>
      <PageHeader
        title={`Manual payment ${payment.id}`}
        description={`Organization ${payment.organizationId}`}
        actions={<Badge tone={statusTone(status)}>{status}</Badge>}
      />
      {mutation.error ? (
        <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
      ) : null}
      <DetailGrid
        data={{
          ...payment,
          paymentMethod: enumCode("paymentMethod", payment.paymentMethod),
          status,
        }}
      />
      {administrator ? (
        <Card className="mt-5 flex flex-wrap gap-2">
          {status === "Pending" ? (
            <>
              <Button onClick={() => changeStatus("confirm")}>Confirm</Button>
              <Button variant="danger" onClick={() => changeStatus("reject")}>
                Reject
              </Button>
            </>
          ) : null}
          {status === "Confirmed" ? (
            <Button variant="danger" onClick={() => changeStatus("refund")}>
              Refund
            </Button>
          ) : null}
        </Card>
      ) : (
        <Alert tone="info">
          Support users can inspect payments but cannot change their status.
        </Alert>
      )}
    </>
  );
}
