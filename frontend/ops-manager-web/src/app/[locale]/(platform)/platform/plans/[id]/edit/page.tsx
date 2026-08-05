import { PlatformPlanForm } from "@/features/platform/platform-pages";

export default async function EditPlatformPlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformPlanForm id={id} />;
}
