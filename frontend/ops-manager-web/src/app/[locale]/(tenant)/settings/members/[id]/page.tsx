import { MemberForm } from "@/features/organization/member-management";

export default async function MemberPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <MemberForm id={id} />;
}
