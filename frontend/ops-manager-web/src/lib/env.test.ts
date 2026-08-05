import { publicEnvironmentSchema } from "./env";

describe("public environment", () => {
  it("accepts a valid API URL and supported locale", () => {
    expect(
      publicEnvironmentSchema.parse({
        NEXT_PUBLIC_API_BASE_URL: "https://api.example.com/api/v1",
        NEXT_PUBLIC_DEFAULT_LOCALE: "ar",
      }),
    ).toEqual({
      NEXT_PUBLIC_API_BASE_URL: "https://api.example.com/api/v1",
      NEXT_PUBLIC_DEFAULT_LOCALE: "ar",
    });
  });

  it("rejects unsafe API URL values", () => {
    expect(() =>
      publicEnvironmentSchema.parse({
        NEXT_PUBLIC_API_BASE_URL: "not-a-url",
      }),
    ).toThrow();
  });
});
