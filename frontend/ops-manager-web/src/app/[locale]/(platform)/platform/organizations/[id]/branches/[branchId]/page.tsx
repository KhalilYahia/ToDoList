import { PlatformBranchForm } from "@/features/platform/platform-branch-pages";

export default async function OrganizationBranchPage({
  params,
}: {
  params: Promise<{ id: string; branchId: string }>;
}) {
  const { id, branchId } = await params;
  return <PlatformBranchForm organizationId={id} branchId={branchId} />;
}
