import { getTranslations } from "next-intl/server";

import { AuthShell } from "@/components/layout/auth-shell";
import { AuthForm } from "@/features/auth/auth-form";

export default async function LoginPage() {
  const t = await getTranslations("Auth");
  return (
    <AuthShell
      title={t("loginTitle")}
      subtitle=""
    >
      <AuthForm mode="tenant-login" />
    </AuthShell>
  );
}
