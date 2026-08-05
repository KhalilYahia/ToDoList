"use client";

import { useLocale } from "next-intl";

import { usePathname, useRouter } from "@/i18n/navigation";
import { Select } from "@/components/ui/primitives";
import type { AppLocale } from "@/i18n/routing";

export function LocaleSwitcher() {
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();
  return (
    <label>
      <span className="sr-only">Language</span>
      <Select
        className="min-h-9 w-auto py-1"
        aria-label="Language"
        value={locale}
        onChange={(event) =>
          router.replace(pathname, { locale: event.target.value as AppLocale })
        }
      >
        <option value="en">English</option>
        <option value="ru">Русский</option>
        <option value="ar">العربية</option>
      </Select>
    </label>
  );
}
