import { getTranslations } from "next-intl/server";

import { AuthShell } from "@/components/layout/auth-shell";
import { AuthForm } from "@/features/auth/auth-form";

export default async function RegisterPage() {
  const t = await getTranslations("Auth");
  return (
    <AuthShell
      wide
      title={t("registerTitle")}
      subtitle="Create the organization and its first manager. Registration starts the configured trial and signs you in."
    >
      <AuthForm mode="register" />
    </AuthShell>
  );
}
