import { TaskTemplateDetail } from "@/features/tasks/task-pages";

export default async function TaskTemplatePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <TaskTemplateDetail id={id} />;
}
