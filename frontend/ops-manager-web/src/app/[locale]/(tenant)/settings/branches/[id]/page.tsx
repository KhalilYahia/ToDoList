import { BranchDetail } from "@/features/organization/branch-detail";

export default async function BranchPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <BranchDetail id={id} />;
}
