import { PlatformBranchForm } from "@/features/platform/platform-branch-pages";

export default async function NewOrganizationBranchPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformBranchForm organizationId={id} />;
}
