import { defineRouting } from "next-intl/routing";

export const routing = defineRouting({
  locales: ["ar", "en", "ru"],
  defaultLocale: "en",
  localePrefix: "always",
});

export type AppLocale = (typeof routing.locales)[number];

export function isAppLocale(locale: string): locale is AppLocale {
  return routing.locales.includes(locale as AppLocale);
}

export function directionForLocale(locale: string): "rtl" | "ltr" {
  return locale === "ar" ? "rtl" : "ltr";
}
