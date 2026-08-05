import { z } from "zod";

const publicEnvironmentSchema = z.object({
  NEXT_PUBLIC_API_BASE_URL: z
    .string()
    .url()
    .default("http://localhost:8080/api/v1"),
  NEXT_PUBLIC_DEFAULT_LOCALE: z.enum(["ar", "en", "ru"]).default("en"),
});

export const publicEnvironment = publicEnvironmentSchema.parse({
  NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
  NEXT_PUBLIC_DEFAULT_LOCALE: process.env.NEXT_PUBLIC_DEFAULT_LOCALE,
});

export { publicEnvironmentSchema };
