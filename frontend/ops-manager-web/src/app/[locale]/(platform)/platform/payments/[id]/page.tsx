import { PlatformPaymentDetail } from "@/features/platform/platform-pages";

export default async function PlatformPaymentPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlatformPaymentDetail id={id} />;
}
