import { DepartmentForm } from "@/features/organization/department-management";

export default async function DepartmentPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <DepartmentForm id={id} />;
}
