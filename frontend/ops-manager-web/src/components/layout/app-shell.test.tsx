import type { PropsWithChildren } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { NavLinks, isNavigationItemActive } from "./app-shell";

vi.mock("@/i18n/navigation", () => ({
  Link: ({ href, children, ...props }: PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => "/settings/departments",
  useRouter: () => ({ replace: vi.fn() }),
}));

function TestIcon({ className }: { className?: string }) {
  return <span aria-hidden="true" className={className} />;
}

describe("tenant navigation", () => {
  it("recognizes nested resource routes as active", () => {
    expect(
      isNavigationItemActive("/settings/members/member-1", {
        href: "/settings/members",
        label: "Members",
        icon: TestIcon,
      }),
    ).toBe(true);
  });

  it("opens and collapses the active organization configuration group", async () => {
    const user = userEvent.setup();
    render(
      <NavLinks
        entries={[
          {
            label: "Organization configs",
            icon: TestIcon,
            items: [
              {
                href: "/settings/organization",
                label: "Organization",
                icon: TestIcon,
              },
              {
                href: "/settings/departments",
                label: "Departments",
                icon: TestIcon,
              },
              {
                href: "/settings/members",
                label: "Members",
                icon: TestIcon,
              },
            ],
          },
        ]}
      />,
    );

    const trigger = screen.getByRole("button", {
      name: "Organization configs",
    });
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("link", { name: "Departments" })).toHaveAttribute(
      "aria-current",
      "page",
    );

    await user.click(trigger);

    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(
      screen.queryByRole("link", { name: "Departments" }),
    ).not.toBeInTheDocument();
  });
});
