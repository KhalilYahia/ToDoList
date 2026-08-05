import { OrderTemplateDetail } from "@/features/orders/order-pages";

export default async function OrderTemplatePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <OrderTemplateDetail id={id} />;
}
