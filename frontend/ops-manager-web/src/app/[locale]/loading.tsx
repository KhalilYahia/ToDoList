import { Skeleton } from "@/components/ui/primitives";

export default function Loading() {
  return (
    <main className="mx-auto grid min-h-screen max-w-7xl gap-4 p-6">
      <Skeleton className="h-16" />
      <div className="grid gap-4 md:grid-cols-3">
        <Skeleton className="h-40" />
        <Skeleton className="h-40" />
        <Skeleton className="h-40" />
      </div>
    </main>
  );
}
