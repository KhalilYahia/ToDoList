"use client";

import { useEffect } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { PageHeader } from "@/components/layout/page-header";
import {
  Alert,
  Button,
  Card,
  Field,
  Input,
  Select,
  Skeleton,
} from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

const organizationSchema = z.object({
  name: z.string().trim().min(2).max(200),
  legalName: z.string().trim().max(250),
  logoUrl: z.union([z.literal(""), z.string().url()]),
  phone: z.string().trim().max(40),
  email: z.union([z.literal(""), z.string().email()]),
  timezone: z.string().min(1).max(100),
  defaultLanguage: z.enum(["ar", "en", "ru"]),
});
type OrganizationValues = z.infer<typeof organizationSchema>;

export function OrganizationSettings() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const query = useQuery({
    queryKey: queryKeys.organization.root,
    queryFn: () => apiRequest<Schemas["OrganizationDto"]>("/organization"),
  });
  const form = useForm<OrganizationValues>({
    resolver: zodResolver(organizationSchema),
    defaultValues: {
      name: "",
      legalName: "",
      logoUrl: "",
      phone: "",
      email: "",
      timezone: "UTC",
      defaultLanguage: "en",
    },
  });

  useEffect(() => {
    if (!query.data) return;
    form.reset({
      name: query.data.name,
      legalName: query.data.legalName ?? "",
      logoUrl: query.data.logoUrl ?? "",
      phone: query.data.phone ?? "",
      email: query.data.email ?? "",
      timezone: query.data.timezone,
      defaultLanguage: query.data.defaultLanguage as "ar" | "en" | "ru",
    });
  }, [form, query.data]);

  const mutation = useMutation({
    mutationFn: (values: OrganizationValues) =>
      apiRequest<Schemas["OrganizationDto"]>("/organization", {
        method: "PATCH",
        body: {
          ...values,
          legalName: values.legalName || null,
          logoUrl: values.logoUrl || null,
          phone: values.phone || null,
          email: values.email || null,
        },
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.organization.root, data);
      toast.push("Organization settings saved.");
    },
  });

  return (
    <>
      <PageHeader
        title="Organization settings"
        description="System UI language and timezone apply to organization defaults; user-created content is unchanged."
      />
      {query.isLoading ? (
        <Skeleton className="h-96" />
      ) : (
        <Card className="max-w-4xl">
          <form
            className="grid gap-4 md:grid-cols-2"
            onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
          >
            {mutation.error ? (
              <div className="md:col-span-2">
                <Alert tone="danger">{errorMessage(mutation.error)}</Alert>
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
              label="Legal name"
              error={form.formState.errors.legalName?.message}
            >
              <Input {...form.register("legalName")} />
            </Field>
            <Field
              label="Contact email"
              error={form.formState.errors.email?.message}
            >
              <Input type="email" {...form.register("email")} />
            </Field>
            <Field
              label="Contact phone"
              error={form.formState.errors.phone?.message}
            >
              <Input type="tel" {...form.register("phone")} />
            </Field>
            <Field
              label="Timezone"
              error={form.formState.errors.timezone?.message}
              required
            >
              <Input {...form.register("timezone")} />
            </Field>
            <Field label="Default UI language" required>
              <Select {...form.register("defaultLanguage")}>
                <option value="en">English</option>
                <option value="ru">Русский</option>
                <option value="ar">العربية</option>
              </Select>
            </Field>
            <div className="md:col-span-2">
              <Field
                label="Logo URL"
                hint="The backend currently accepts URL metadata; it has no organization-logo upload endpoint."
                error={form.formState.errors.logoUrl?.message}
              >
                <Input type="url" {...form.register("logoUrl")} />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Button type="submit" busy={mutation.isPending}>
                Save organization
              </Button>
            </div>
          </form>
        </Card>
      )}
    </>
  );
}
