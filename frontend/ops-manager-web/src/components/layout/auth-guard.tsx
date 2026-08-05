"use client";

import { useEffect, useState, type ReactNode } from "react";
import { LoaderCircle, ShieldAlert } from "lucide-react";

import { Alert, Button, Card } from "@/components/ui/primitives";
import { useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/auth-provider";
import type { AuthRealm } from "@/lib/auth/session-store";
import { subscriptionAccessMode } from "@/lib/permissions/permissions";

export function AuthGuard({
  realm,
  children,
}: {
  realm: AuthRealm;
  children: ReactNode;
}) {
  const { identity, bootstrap } = useAuth();
  const [checkedRealm, setCheckedRealm] = useState<AuthRealm | null>(null);
  const router = useRouter();
  const authenticated = identity?.realm === realm;

  useEffect(() => {
    let active = true;
    if (authenticated) return;
    void bootstrap(realm).then((nextIdentity) => {
      if (!active) return;
      setCheckedRealm(realm);
      if (nextIdentity?.realm !== realm) {
        router.replace(realm === "platform" ? "/platform/login" : "/login");
      }
    });
    return () => {
      active = false;
    };
  }, [authenticated, bootstrap, realm, router]);

  if (!authenticated && checkedRealm !== realm) {
    return (
      <main className="grid min-h-screen place-items-center">
        <div
          className="text-ink-600 flex items-center gap-3 text-sm"
          role="status"
        >
          <LoaderCircle className="size-5 animate-spin" />
          Restoring session…
        </div>
      </main>
    );
  }

  if (identity?.realm !== realm) return null;

  if (
    realm === "tenant" &&
    identity.realm === "tenant" &&
    subscriptionAccessMode(identity) === "Blocked"
  ) {
    return (
      <main className="grid min-h-screen place-items-center p-6">
        <Card className="max-w-lg">
          <ShieldAlert className="text-danger-700 mb-4 size-10" />
          <Alert title="Organization access is blocked" tone="danger">
            {identity.session.access.reason ??
              "Contact the platform administrator for assistance."}
          </Alert>
          <Button className="mt-5" onClick={() => router.replace("/login")}>
            Return to sign in
          </Button>
        </Card>
      </main>
    );
  }

  return children;
}
