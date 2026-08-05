import { OrderDetail } from "@/features/orders/order-pages";

export default async function DepartmentOrderPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <OrderDetail id={id} />;
}
