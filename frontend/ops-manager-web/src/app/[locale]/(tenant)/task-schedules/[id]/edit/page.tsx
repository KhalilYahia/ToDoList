import { TaskScheduleForm } from "@/features/tasks/task-schedule-form";

export default async function EditTaskSchedulePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <TaskScheduleForm id={id} />;
}
