import { directionForLocale, isAppLocale } from "./routing";

describe("locale routing", () => {
  it("uses RTL only for Arabic", () => {
    expect(directionForLocale("ar")).toBe("rtl");
    expect(directionForLocale("en")).toBe("ltr");
    expect(directionForLocale("ru")).toBe("ltr");
  });

  it("accepts only supported locales", () => {
    expect(isAppLocale("ru")).toBe(true);
    expect(isAppLocale("de")).toBe(false);
  });
});
