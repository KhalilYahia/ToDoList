import { TaskTemplateForm } from "@/features/tasks/task-template-form";

export default async function EditTaskTemplatePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <TaskTemplateForm id={id} />;
}
