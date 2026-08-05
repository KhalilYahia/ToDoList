import { PlatformBranchesPage } from "@/features/platform/platform-branch-pages";

export default async function OrganizationBranchesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformBranchesPage organizationId={id} />;
}
