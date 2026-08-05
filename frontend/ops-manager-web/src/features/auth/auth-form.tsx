"use client";

import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Building2, ShieldCheck } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";

import {
  Alert,
  Button,
  Field,
  Input,
  Select,
} from "@/components/ui/primitives";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/auth-provider";
import { errorMessage } from "@/lib/api/errors";

const tenantLoginSchema = z.object({
  organizationId: z.string().optional(),
  email: z.string().email("Enter a valid email address."),
  password: z.string().min(8, "Enter your password."),
});

const platformLoginSchema = z.object({
  email: z.string().email("Enter a valid email address."),
  password: z.string().min(8, "Enter your password."),
});

const registrationSchema = z.object({
  organizationName: z.string().trim().min(2).max(200),
  legalName: z.string().trim().max(250).optional(),
  timezone: z.string().min(1),
  defaultLanguage: z.enum(["ar", "en", "ru"]),
  managerFullName: z.string().trim().min(2).max(200),
  managerEmail: z.string().email(),
  password: z.string().min(12).max(128),
  phone: z.string().trim().max(40).optional(),
});

type TenantLoginValues = z.infer<typeof tenantLoginSchema>;
type PlatformLoginValues = z.infer<typeof platformLoginSchema>;
type RegistrationInput = z.input<typeof registrationSchema>;

function TenantLoginForm() {
  const t = useTranslations("Auth");
  const { loginTenant } = useAuth();
  const router = useRouter();
  const [requestError, setRequestError] = useState<string>();
  const form = useForm<TenantLoginValues>({
    resolver: zodResolver(tenantLoginSchema),
    defaultValues: { organizationId: "", email: "", password: "" },
  });

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={form.handleSubmit(async (values) => {
        setRequestError(undefined);
        try {
          await loginTenant(values);
          router.replace("/dashboard");
        } catch (error) {
          setRequestError(errorMessage(error));
        }
      })}
    >
      {requestError ? <Alert tone="danger">{requestError}</Alert> : null}
      <Field
        label={t("managerEmail")}
        error={form.formState.errors.email?.message}
        required
      >
        <Input type="email" autoComplete="email" {...form.register("email")} />
      </Field>
      <Field
        label={t("password")}
        error={form.formState.errors.password?.message}
        required
      >
        <Input
          type="password"
          autoComplete="current-password"
          {...form.register("password")}
        />
      </Field>
      <Button type="submit" busy={form.formState.isSubmitting}>
        {t("login")} <ArrowRight className="size-4 rtl:rotate-180" />
      </Button>
      <Link
        href="/register"
        className="text-brand-700 text-center text-sm font-semibold hover:underline"
      >
        {t("registerLink")}
      </Link>
    </form>
  );
}

function PlatformLoginForm() {
  const t = useTranslations("Auth");
  const { loginPlatform } = useAuth();
  const router = useRouter();
  const [requestError, setRequestError] = useState<string>();
  const form = useForm<PlatformLoginValues>({
    resolver: zodResolver(platformLoginSchema),
    defaultValues: { email: "", password: "" },
  });

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={form.handleSubmit(async (values) => {
        setRequestError(undefined);
        try {
          await loginPlatform(values);
          router.replace("/platform");
        } catch (error) {
          setRequestError(errorMessage(error));
        }
      })}
    >
      {requestError ? <Alert tone="danger">{requestError}</Alert> : null}
      <Field
        label={t("managerEmail")}
        error={form.formState.errors.email?.message}
        required
      >
        <Input type="email" autoComplete="email" {...form.register("email")} />
      </Field>
      <Field
        label={t("password")}
        error={form.formState.errors.password?.message}
        required
      >
        <Input
          type="password"
          autoComplete="current-password"
          {...form.register("password")}
        />
      </Field>
      <Button type="submit" busy={form.formState.isSubmitting}>
        {t("login")} <ShieldCheck className="size-4" />
      </Button>
    </form>
  );
}

function RegistrationForm() {
  const t = useTranslations("Auth");
  const locale = useLocale();
  const { registerTenant } = useAuth();
  const router = useRouter();
  const [requestError, setRequestError] = useState<string>();
  const timezone = useMemo(
    () => Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    [],
  );
  const form = useForm<RegistrationInput>({
    resolver: zodResolver(registrationSchema),
    defaultValues: {
      organizationName: "",
      legalName: "",
      timezone,
      defaultLanguage: ["ar", "en", "ru"].includes(locale)
        ? (locale as "ar" | "en" | "ru")
        : "en",
      managerFullName: "",
      managerEmail: "",
      password: "",
      phone: "",
    },
  });

  useEffect(() => {
    form.setValue("timezone", timezone);
  }, [form, timezone]);

  return (
    <form
      className="grid gap-4 md:grid-cols-2"
      noValidate
      onSubmit={form.handleSubmit(async (input) => {
        setRequestError(undefined);
        try {
          const parsed = registrationSchema.parse(input);
          const values = {
            ...parsed,
            legalName: parsed.legalName || null,
            phone: parsed.phone || null,
          };
          await registerTenant(values);
          router.replace("/dashboard");
        } catch (error) {
          setRequestError(errorMessage(error));
        }
      })}
    >
      {requestError ? (
        <div className="md:col-span-2">
          <Alert tone="danger">{requestError}</Alert>
        </div>
      ) : null}
      <Field
        label={t("organizationName")}
        error={form.formState.errors.organizationName?.message}
        required
      >
        <Input
          autoComplete="organization"
          {...form.register("organizationName")}
        />
      </Field>
      <Field
        label={t("legalName")}
        error={form.formState.errors.legalName?.message}
      >
        <Input {...form.register("legalName")} />
      </Field>
      <Field
        label={t("timezone")}
        error={form.formState.errors.timezone?.message}
        required
      >
        <Input autoComplete="off" {...form.register("timezone")} />
      </Field>
      <Field
        label={t("defaultLanguage")}
        error={form.formState.errors.defaultLanguage?.message}
        required
      >
        <Select {...form.register("defaultLanguage")}>
          <option value="en">English</option>
          <option value="ru">Русский</option>
          <option value="ar">العربية</option>
        </Select>
      </Field>
      <Field
        label={t("managerName")}
        error={form.formState.errors.managerFullName?.message}
        required
      >
        <Input autoComplete="name" {...form.register("managerFullName")} />
      </Field>
      <Field
        label={t("managerEmail")}
        error={form.formState.errors.managerEmail?.message}
        required
      >
        <Input
          type="email"
          autoComplete="email"
          {...form.register("managerEmail")}
        />
      </Field>
      <Field
        label={t("password")}
        error={form.formState.errors.password?.message}
        required
      >
        <Input
          type="password"
          autoComplete="new-password"
          {...form.register("password")}
        />
      </Field>
      <Field
        label={t("managerPhone")}
        error={form.formState.errors.phone?.message}
      >
        <Input type="tel" autoComplete="tel" {...form.register("phone")} />
      </Field>
      <div className="grid gap-3 md:col-span-2">
        <Button type="submit" busy={form.formState.isSubmitting}>
          {t("register")} <Building2 className="size-4" />
        </Button>
        <Link
          href="/login"
          className="text-brand-700 text-center text-sm font-semibold hover:underline"
        >
          {t("loginLink")}
        </Link>
      </div>
    </form>
  );
}

export function AuthForm({
  mode,
}: {
  mode: "tenant-login" | "platform-login" | "register";
}) {
  if (mode === "tenant-login") return <TenantLoginForm />;
  if (mode === "platform-login") return <PlatformLoginForm />;
  return <RegistrationForm />;
}
