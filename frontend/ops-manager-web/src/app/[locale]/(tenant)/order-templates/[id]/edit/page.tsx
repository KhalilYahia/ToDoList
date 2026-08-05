import { OrderTemplateForm } from "@/features/orders/order-pages";

export default async function EditOrderTemplatePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <OrderTemplateForm id={id} />;
}
