import type { ReactNode } from "react";
import { CheckCircle2 } from "lucide-react";

import { Card } from "@/components/ui/primitives";
import { LocaleSwitcher } from "@/components/layout/locale-switcher";

export function AuthShell({
  title,
  subtitle,
  children,
  wide = false,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
  wide?: boolean;
}) {
  return (
    <main
      id="main-content"
      className="grid min-h-screen lg:grid-cols-[minmax(20rem,0.8fr)_minmax(28rem,1.2fr)]"
    >
      <section className="bg-brand-700 relative hidden overflow-hidden p-10 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="absolute -start-20 top-16 size-80 rounded-full border-[4rem] border-white/5" />
        <p className="relative text-xl font-black tracking-tight">OpsManager</p>
        <div className="relative">
          <p className="max-w-md text-4xl leading-tight font-black">
            One calm place for the work that keeps moving.
          </p>
          <ul className="mt-8 grid gap-3 text-sm text-white/85">
            {[
              "Tasks and approvals",
              "Department orders",
              "Private complaints",
            ].map((item) => (
              <li key={item} className="flex items-center gap-2">
                <CheckCircle2 className="size-4" /> {item}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative text-xs text-white/60">
          Arabic · English · Russian
        </p>
      </section>
      <section className="grid place-items-center p-5 md:p-10">
        <div className={wide ? "w-full max-w-3xl" : "w-full max-w-md"}>
          <div className="mb-5 flex justify-end">
            <LocaleSwitcher />
          </div>
          <Card className="p-6 md:p-8">
            <p className="text-brand-700 mb-2 text-sm font-bold tracking-[0.16em] uppercase">
              OpsManager
            </p>
            <h1 className="text-2xl font-black tracking-tight md:text-3xl">
              {title}
            </h1>
            <p className="text-ink-600 mt-2 mb-7 text-sm leading-6">
              {subtitle}
            </p>
            {children}
          </Card>
        </div>
      </section>
    </main>
  );
}
