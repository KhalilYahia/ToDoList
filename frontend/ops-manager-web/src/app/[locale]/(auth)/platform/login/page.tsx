import { getTranslations } from "next-intl/server";

import { AuthShell } from "@/components/layout/auth-shell";
import { AuthForm } from "@/features/auth/auth-form";

export default async function PlatformLoginPage() {
  const t = await getTranslations("Auth");
  return (
    <AuthShell
      title={t("platformLoginTitle")}
      subtitle="This session is separate from organization access and is limited to platform identities."
    >
      <AuthForm mode="platform-login" />
    </AuthShell>
  );
}
