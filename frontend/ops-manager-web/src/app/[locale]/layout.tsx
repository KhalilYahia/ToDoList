import type { Metadata } from "next";
import { hasLocale, NextIntlClientProvider } from "next-intl";
import { getMessages, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import type { ReactNode } from "react";

import { directionForLocale, routing } from "@/i18n/routing";

import "./globals.css";
import { Providers } from "./providers";

export const metadata: Metadata = {
  title: {
    default: "OpsManager",
    template: "%s · OpsManager",
  },
  description: "Operations, tasks, orders, complaints, and subscriptions.",
};

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export default async function LocaleLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);
  const messages = await getMessages();

  return (
    <html lang={locale} dir={directionForLocale(locale)}>
      <body>
        <a
          href="#main-content"
          className="bg-ink-950 fixed start-4 top-2 z-[100] -translate-y-20 rounded-lg px-4 py-2 text-white focus:translate-y-0"
        >
          Skip to content
        </a>
        <NextIntlClientProvider messages={messages}>
          <Providers>{children}</Providers>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
