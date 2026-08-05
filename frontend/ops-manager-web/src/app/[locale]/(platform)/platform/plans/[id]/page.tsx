import { PlatformPlanDetail } from "@/features/platform/platform-pages";

export default async function PlatformPlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformPlanDetail id={id} />;
}
