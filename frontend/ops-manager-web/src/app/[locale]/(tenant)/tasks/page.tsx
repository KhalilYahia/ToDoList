import { redirect } from "@/i18n/navigation";

export default async function TasksIndex({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  redirect({ href: "/tasks/upcoming", locale });
}
