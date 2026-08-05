import type { ReactNode } from "react";

import { AppShell } from "@/components/layout/app-shell";
import { AuthGuard } from "@/components/layout/auth-guard";

export default function PlatformLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard realm="platform">
      <AppShell realm="platform">{children}</AppShell>
    </AuthGuard>
  );
}
