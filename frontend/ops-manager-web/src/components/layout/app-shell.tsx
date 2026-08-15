"use client";

import { useState, type ComponentType, type ReactNode } from "react";
import {
  BarChart3,
  Bell,
  Building2,
  CalendarDays,
  ChevronDown,
  ClipboardCheck,
  CreditCard,
  FileWarning,
  LayoutDashboard,
  LogOut,
  Menu,
  PackageCheck,
  Settings,
  ShieldCheck,
  Users,
} from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useQuery } from "@tanstack/react-query";

import { Alert, Badge, Button } from "@/components/ui/primitives";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { enumCode } from "@/lib/api/enums";
import type { Schemas } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/auth-provider";
import {
  isManager,
  isPlatformAdministrator,
  isSupervisorOrManager,
  subscriptionAccessMode,
  tenantRole,
} from "@/lib/permissions/permissions";
import { queryKeys } from "@/lib/query/query-keys";
import { cn, formatDateTime } from "@/lib/utils";

import { LocaleSwitcher } from "./locale-switcher";

type NavigationItem = {
  href: string;
  label: string;
  icon: ComponentType<{ className?: string }>;
};

type NavigationGroup = {
  label: string;
  icon: ComponentType<{ className?: string }>;
  items: NavigationItem[];
};

type NavigationEntry = NavigationItem | NavigationGroup;

export function isNavigationItemActive(
  pathname: string,
  item: NavigationItem,
): boolean {
  return (
    pathname === item.href ||
    (item.href !== "/dashboard" && pathname.startsWith(`${item.href}/`))
  );
}

function NavigationLink({
  item,
  nested = false,
}: {
  item: NavigationItem;
  nested?: boolean;
}) {
  const pathname = usePathname();
  const active = isNavigationItemActive(pathname, item);

  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm font-semibold transition",
        nested && "ms-4 min-h-10",
        active
          ? "bg-brand-100 text-brand-700"
          : "text-ink-800 hover:bg-ink-950/5",
      )}
    >
      <item.icon className="size-4.5 shrink-0" />
      {item.label}
    </Link>
  );
}

function CollapsibleNavigationGroup({ group }: { group: NavigationGroup }) {
  const pathname = usePathname();
  const groupActive =
    group.items.some((item) => isNavigationItemActive(pathname, item)) ||
    (pathname.startsWith("/tasks/") &&
      group.items.some((item) => item.href.startsWith("/tasks"))) ||
    (pathname === "/my-tasks" &&
      group.items.some((item) => item.href === "/my-tasks"));
  const [state, setState] = useState({
    pathname,
    open: groupActive,
  });
  const open = state.pathname === pathname ? state.open : groupActive;

  return (
    <div>
      <button
        type="button"
        aria-expanded={open}
        className={cn(
          "flex min-h-11 w-full items-center gap-3 rounded-xl px-3 text-start text-sm font-semibold transition",
          groupActive ? "text-brand-700" : "text-ink-800 hover:bg-ink-950/5",
        )}
        onClick={() => setState({ pathname, open: !open })}
      >
        <group.icon className="size-4.5 shrink-0" />
        <span className="min-w-0 flex-1">{group.label}</span>
        <ChevronDown
          aria-hidden="true"
          className={cn(
            "size-4 shrink-0 transition-transform",
            open && "rotate-180",
          )}
        />
      </button>
      {open ? (
        <div className="mt-1 grid gap-1">
          {group.items.map((item) => (
            <NavigationLink key={item.href} item={item} nested />
          ))}
        </div>
      ) : null}
    </div>
  );
}

export function NavLinks({ entries }: { entries: NavigationEntry[] }) {
  return (
    <nav aria-label="Primary" className="grid gap-1">
      {entries.map((entry) =>
        "href" in entry ? (
          <NavigationLink key={entry.href} item={entry} />
        ) : (
          <CollapsibleNavigationGroup key={entry.label} group={entry} />
        ),
      )}
    </nav>
  );
}

export function AppShell({
  realm,
  children,
}: {
  realm: "tenant" | "platform";
  children: ReactNode;
}) {
  const t = useTranslations("Navigation");
  const { identity, logout } = useAuth();
  const router = useRouter();
  const locale = useLocale();

  const unreadQuery = useQuery({
    queryKey: queryKeys.notifications.unread,
    queryFn: () =>
      apiRequest<Schemas["UnreadNotificationCountDto"]>(
        "/notifications/unread-count",
      ),
    enabled: realm === "tenant" && identity?.realm === "tenant",
    refetchInterval: 60_000,
  });

  const tenantItems: NavigationEntry[] = [
    { href: "/dashboard", label: t("dashboard"), icon: LayoutDashboard },
    ...(isManager(identity)
      ? [
          {
            href: "/task-templates",
            label: t("taskTemplates"),
            icon: ClipboardCheck,
          },
          {
            href: "/task-schedules",
            label: t("taskSchedules"),
            icon: CalendarDays,
          },
        ]
      : []),
    {
      label: t("tasks"),
      icon: ClipboardCheck,
      items: [
        {
          href: "/tasks/upcoming",
          label: t("upcomingTasks"),
          icon: ClipboardCheck,
        },
        {
          href: "/tasks/past",
          label: t("pastTasks"),
          icon: ClipboardCheck,
        },
        ...(isSupervisorOrManager(identity)
          ? [
              {
                href: "/my-tasks",
                label: t("myTasks"),
                icon: ClipboardCheck,
              },
            ]
          : []),
      ],
    },
    { href: "/department-orders", label: t("orders"), icon: PackageCheck },
    ...(isManager(identity)
      ? [
          {
            href: "/order-templates",
            label: t("orderTemplates"),
            icon: PackageCheck,
          },
        ]
      : []),
    { href: "/complaints", label: t("complaints"), icon: FileWarning },
    ...(isManager(identity)
      ? [
          { href: "/reports/tasks", label: t("reports"), icon: BarChart3 },
          {
            label: t("organizationConfigs"),
            icon: Settings,
            items: [
              {
                href: "/settings/organization",
                label: t("organization"),
                icon: Building2,
              },
              {
                href: "/settings/departments",
                label: t("departments"),
                icon: Building2,
              },
              {
                href: "/settings/members",
                label: t("members"),
                icon: Users,
              },
            ],
          },
          {
            href: "/settings/subscription",
            label: t("subscription"),
            icon: CreditCard,
          },
        ]
      : []),
  ];

  const platformItems: NavigationItem[] = [
    { href: "/platform", label: t("platform"), icon: ShieldCheck },
    {
      href: "/platform/organizations",
      label: t("organizations"),
      icon: Building2,
    },
    { href: "/platform/plans", label: t("plans"), icon: CreditCard },
    { href: "/platform/payments", label: t("payments"), icon: CreditCard },
    { href: "/platform/reports", label: t("reports"), icon: BarChart3 },
  ];

  const entries = realm === "tenant" ? tenantItems : platformItems;
  const tenantIdentity = identity?.realm === "tenant" ? identity : null;
  const platformIdentity = identity?.realm === "platform" ? identity : null;
  const access = tenantIdentity?.session.access;

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[17rem_1fr]">
      <aside className="border-ink-950/8 bg-surface/85 hidden flex-col border-e p-4 backdrop-blur lg:sticky lg:top-0 lg:flex lg:h-screen">
        <Link
          href={realm === "tenant" ? "/dashboard" : "/platform"}
          className="mb-6 flex items-center gap-3 px-2 shrink-0"
        >
          <span className="bg-brand-700 grid size-10 place-items-center rounded-xl font-black text-white">
            O
          </span>
          <span>
            <span className="block font-black tracking-tight">OpsManager</span>
            <span className="text-ink-600 block text-xs">
              {realm === "tenant" ? "Workspace" : "Platform console"}
            </span>
          </span>
        </Link>
        <div className="flex-1 overflow-y-auto pe-1">
          <NavLinks entries={entries} />
        </div>
        <div className="mt-auto border-t border-ink-950/8 pt-3 grid gap-1 shrink-0">
          {realm === "tenant" ? (
            <NavigationLink
              item={{
                href: "/settings/profile",
                label: t("profile"),
                icon: Users,
              }}
            />
          ) : null}
          <button
            type="button"
            onClick={() =>
              void logout().then(() =>
                router.replace(
                  realm === "platform" ? "/platform/login" : "/login",
                ),
              )
            }
            className="flex min-h-11 w-full items-center gap-3 rounded-xl px-3 text-start text-sm font-semibold text-danger-600 hover:bg-danger-50 transition"
          >
            <LogOut className="size-4.5 shrink-0" />
            <span>{t("logout")}</span>
          </button>
        </div>
      </aside>

      <div className="min-w-0">
        <header className="border-ink-950/8 bg-canvas/90 sticky top-0 z-30 flex min-h-16 items-center gap-3 border-b px-4 backdrop-blur md:px-6">
          <details className="relative lg:hidden">
            <summary className="border-ink-950/10 bg-surface grid size-10 cursor-pointer list-none place-items-center rounded-xl border">
              <Menu className="size-5" />
              <span className="sr-only">{t("menu")}</span>
            </summary>
            <div className="border-ink-950/10 bg-surface absolute start-0 top-12 z-50 w-72 rounded-2xl border p-3 shadow-2xl">
              <NavLinks entries={entries} />
              <div className="mt-2 border-t border-ink-950/8 pt-2 grid gap-1">
                {realm === "tenant" ? (
                  <NavigationLink
                    item={{
                      href: "/settings/profile",
                      label: t("profile"),
                      icon: Users,
                    }}
                  />
                ) : null}
                <button
                  type="button"
                  onClick={() =>
                    void logout().then(() =>
                      router.replace(
                        realm === "platform" ? "/platform/login" : "/login",
                      ),
                    )
                  }
                  className="flex min-h-11 w-full items-center gap-3 rounded-xl px-3 text-start text-sm font-semibold text-danger-600 hover:bg-danger-50 transition"
                >
                  <LogOut className="size-4.5 shrink-0" />
                  <span>{t("logout")}</span>
                </button>
              </div>
            </div>
          </details>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-bold">
              {tenantIdentity?.session.organization.name ??
                platformIdentity?.user.fullName}
            </p>
            <p className="text-ink-600 truncate text-xs">
              {tenantRole(identity) ??
                (platformIdentity ? String(platformIdentity.user.role) : "")}
            </p>
          </div>
          <LocaleSwitcher />
          {realm === "tenant" ? (
            <Link
              href="/notifications"
              className="border-ink-950/10 bg-surface relative grid size-10 place-items-center rounded-xl border"
              aria-label={t("notifications")}
            >
              <Bell className="size-4.5" />
              {Number(unreadQuery.data?.count ?? 0) > 0 ? (
                <span className="bg-accent-600 absolute -end-1 -top-1 grid min-w-5 place-items-center rounded-full px-1 text-[10px] font-bold text-white">
                  {unreadQuery.data?.count}
                </span>
              ) : null}
            </Link>
          ) : null}
          {platformIdentity && isPlatformAdministrator(identity) ? (
            <Badge tone="success">Administrator</Badge>
          ) : null}
          <Button
            variant="ghost"
            size="sm"
            onClick={() =>
              void logout().then(() =>
                router.replace(
                  realm === "platform" ? "/platform/login" : "/login",
                ),
              )
            }
          >
            <LogOut className="size-4" />
            <span className="hidden md:inline">{t("logout")}</span>
          </Button>
        </header>

        {tenantIdentity && access ? (
          <div className="px-4 pt-4 md:px-6">
            {subscriptionAccessMode(identity) === "ReadOnly" ? (
              <Alert tone="danger">
                {access.reason ??
                  "Your organization is read-only. Changes are disabled."}
              </Alert>
            ) : subscriptionAccessMode(identity) === "GraceLimited" ? (
              <Alert tone="warning">
                {access.reason ?? "The subscription is in its grace period."}{" "}
                {access.expiresAt
                  ? formatDateTime(access.expiresAt, locale)
                  : null}
              </Alert>
            ) : enumCode("subscriptionStatus", access.status) === "Trial" &&
              access.expiresAt ? (
              <Alert tone="info">
                Trial access ends {formatDateTime(access.expiresAt, locale)}.
              </Alert>
            ) : null}
          </div>
        ) : null}

        <main
          id="main-content"
          className="mx-auto w-full max-w-[96rem] p-4 md:p-6"
        >
          {children}
        </main>
      </div>
    </div>
  );
}
