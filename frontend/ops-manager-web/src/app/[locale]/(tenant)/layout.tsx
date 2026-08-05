import type { ReactNode } from "react";

import { AppShell } from "@/components/layout/app-shell";
import { AuthGuard } from "@/components/layout/auth-guard";

export default function TenantLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard realm="tenant">
      <AppShell realm="tenant">{children}</AppShell>
    </AuthGuard>
  );
}
