"use client";
import { useState } from "react";
import { useLocale } from "next-intl";
import { useMutation } from "@tanstack/react-query";

import { LocaleSwitcher } from "@/components/layout/locale-switcher";
import { PageHeader } from "@/components/layout/page-header";
import { Alert, Badge, Button, Card, Field, FileUploader, Input } from "@/components/ui/primitives";
import { useToast } from "@/components/ui/toast-provider";
import { apiRequest } from "@/lib/api/client";
import { enumCode, statusTone } from "@/lib/api/enums";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  isManager,
  subscriptionAccessMode,
  tenantRole,
} from "@/lib/permissions/permissions";
import { formatDateTime } from "@/lib/utils";

import { DetailGrid } from "../shared/detail-grid";
export function SubscriptionPage() {
  const locale = useLocale();
  const { identity } = useAuth();
  if (identity?.realm !== "tenant") return null;

  if (!isManager(identity)) {
    return (
      <Alert tone="danger" title="Manager access required">
        Subscription and financial details are restricted to organization
        managers.
      </Alert>
    );
  }

  const access = identity.session.access;
  const mode = subscriptionAccessMode(identity) ?? "Blocked";
  const status =
    access.status !== null
      ? enumCode("subscriptionStatus", access.status)
      : "Unavailable";

  return (
    <>
      <PageHeader
        title="Subscription"
        description="Current tenant access as reported by the authenticated API session."
        actions={
          <>
            <Badge tone={statusTone(status)}>{status}</Badge>
            <Badge tone={statusTone(mode)}>{mode}</Badge>
          </>
        }
      />
      {mode === "ReadOnly" || mode === "Blocked" ? (
        <Alert tone="danger" title="Changes are restricted">
          {access.reason ??
            "The backend currently prevents changes for this organization."}
        </Alert>
      ) : mode === "GraceLimited" ? (
        <Alert tone="warning" title="Grace period">
          {access.reason ?? "Some operations may be restricted."}
        </Alert>
      ) : status === "Trial" ? (
        <Alert tone="info" title="Trial access">
          {access.expiresAt
            ? `Trial access ends ${formatDateTime(access.expiresAt, locale)}.`
            : "The API did not provide a trial end date."}
        </Alert>
      ) : null}
      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <DetailGrid
          data={{
            organization: identity.session.organization.name,
            status,
            accessMode: mode,
            expiresAt: access.expiresAt,
            reason: access.reason,
          }}
        />
        <Card>
          <h2 className="text-lg font-black">Plan and renewal</h2>
          <p className="text-ink-600 mt-3 text-sm">
            The tenant API currently exposes access status only. It does not
            provide the current plan, limits, feature flags, usage, payment
            history, or renewal instructions. Those details remain available
            only in the platform administration API.
          </p>
          <p className="text-ink-600 mt-3 text-sm">
            Online checkout is intentionally not part of this MVP. Contact the
            platform administrator for manual renewal.
          </p>
        </Card>
      </div>
    </>
  );
}

function ChangePasswordCard() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();

  const changeMutation = useMutation({
    mutationFn: () =>
      apiRequest("/auth/change-password", {
        method: "POST",
        body: { currentPassword, newPassword },
      }),
    onSuccess: () => {
      toast.push("Password updated successfully", "success");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setError(null);
    },
    onError: (err) => {
      setError(errorMessage(err));
    },
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!currentPassword) {
      setError("Please enter your current password.");
      return;
    }
    if (newPassword.length < 8) {
      setError("New password must be at least 8 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }
    changeMutation.mutate();
  }
  return (
    <Card>
      <h2 className="text-lg font-black">Change Password</h2>
      <p className="text-ink-600 mt-1 text-sm">
        Update your personal account password.
      </p>
      <form onSubmit={handleSubmit} className="mt-4 grid gap-4">
        {error ? <Alert tone="danger">{error}</Alert> : null}
        <Field label="Current password" required>
          <Input
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
          />
        </Field>
        <Field label="New password" hint="At least 8 characters" required>
          <Input
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
        </Field>
        <Field label="Confirm new password" required>
          <Input
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
          />
        </Field>
        <div>
          <Button type="submit" disabled={changeMutation.isPending}>
            {changeMutation.isPending ? "Updating..." : "Update Password"}
          </Button>
        </div>
      </form>
    </Card>
  );
}


function EditProfileCard() {
  const { identity, refresh } = useAuth();
  const toast = useToast();
  if (identity?.realm !== "tenant") return null;
  const user = identity.session.user;

  const [fullName, setFullName] = useState(user.fullName || "");
  const [phone, setPhone] = useState(user.phone || "");
  const [address, setAddress] = useState((user as any).address || "");
  const [profileImageUrl, setProfileImageUrl] = useState((user as any).profileImageUrl || "");
  const [error, setError] = useState<string | null>(null);

  const profileMutation = useMutation({
    mutationFn: () =>
      apiRequest<Schemas["CurrentUserDto"]>("/auth/profile", {
        method: "PATCH",
        body: {
          fullName,
          phone: phone || null,
          address: address || null,
          profileImageUrl: profileImageUrl || null,
        } satisfies Schemas["UpdateProfileRequest"],
      }),
    onSuccess: async () => {
      toast.push("Profile updated successfully", "success");
      setError(null);
      await refresh();
    },
    onError: (err) => {
      setError(errorMessage(err));
    },
  });

  return (
    <Card>
      <h2 className="text-lg font-black">Edit Profile & Address</h2>
      <p className="text-ink-600 mt-1 text-sm">
        Update your personal details, profile picture, and address.
      </p>

      {/* Profile Photo Uploader */}
      <div className="mt-4 flex flex-wrap items-center gap-4 border-b border-ink-950/10 pb-4">
        <div className="relative size-16 overflow-hidden rounded-full border-2 border-indigo-600 bg-surface-100 shadow-md flex items-center justify-center">
          {profileImageUrl ? (
            <img
              src={profileImageUrl}
              alt="Avatar"
              className="size-full object-cover"
            />
          ) : (
            <span className="text-xl font-black text-indigo-700">
              {(fullName || "U").charAt(0).toUpperCase()}
            </span>
          )}
        </div>
        <div>
          <p className="text-xs font-bold text-ink-900 mb-1">Profile Photo (Фото профиля)</p>
          <FileUploader
            label="Upload new photo"
            onChange={async (file: File | null) => {
              if (!file) return;
              const data = new FormData();
              data.append("file", file);
              try {
                const res = await apiRequest<{ url: string }>("/auth/avatar", {
                  method: "POST",
                  body: data,
                });
                setProfileImageUrl(res.url);
                toast.push("Profile photo uploaded.");
              } catch (err) {
                toast.push("Failed to upload avatar.");
              }
            }}
          />
        </div>
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          profileMutation.mutate();
        }}
        className="mt-4 grid gap-4"
      >
        {error ? <Alert tone="danger">{error}</Alert> : null}
        <Field label="Full name" required>
          <Input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
          />
        </Field>
        <Field label="Phone">
          <Input
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
          />
        </Field>
        <Field label="Address (Адрес)">
          <Input
            placeholder="Enter your address..."
            value={address}
            onChange={(e) => setAddress(e.target.value)}
          />
        </Field>
        <div>
          <Button type="submit" busy={profileMutation.isPending}>
            Save changes
          </Button>
        </div>
      </form>
    </Card>
  );
}

export function ProfilePage() {
  const { identity } = useAuth();
  if (identity?.realm !== "tenant") return null;
  const { user, organization } = identity.session;
  return (
    <>
      <PageHeader
        title="Profile and preferences"
        description="Your authenticated profile and local interface language."
        actions={<Badge>{tenantRole(identity)}</Badge>}
      />
      <div className="grid gap-5 lg:grid-cols-2">
        <DetailGrid
          data={{
            fullName: user.fullName,
            email: user.email,
            phone: user.phone,
            address: (user as any).address,
            organization: organization.name,
            preferredLanguage: user.preferredLanguage,
            accountStatus: enumCode("accountStatus", user.accountStatus),
          }}
        />
        <div className="grid gap-5">
          <EditProfileCard />
          <Card>
            <h2 className="text-lg font-black">Interface language</h2>
            <p className="text-ink-600 mt-2 text-sm">
              Language changes apply to this browser route. User-created names,
              descriptions, and messages are shown exactly as entered.
            </p>
            <div className="border-ink-950/10 mt-4 inline-flex rounded-xl border p-2">
              <LocaleSwitcher />
            </div>
          </Card>
          <ChangePasswordCard />
        </div>
      </div>
    </>
  );
}
