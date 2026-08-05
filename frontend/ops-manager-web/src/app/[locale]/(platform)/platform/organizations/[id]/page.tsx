import { PlatformOrganizationDetail } from "@/features/platform/platform-pages";

export default async function PlatformOrganizationPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformOrganizationDetail id={id} />;
}
