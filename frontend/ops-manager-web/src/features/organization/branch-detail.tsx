"use client";

import { useQuery } from "@tanstack/react-query";

import { PageHeader } from "@/components/layout/page-header";
import { Alert, Button, Skeleton } from "@/components/ui/primitives";
import { Link } from "@/i18n/navigation";
import { apiRequest } from "@/lib/api/client";
import { errorMessage } from "@/lib/api/errors";
import type { Schemas } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

import { DetailGrid } from "../shared/detail-grid";

export function BranchDetail({ id }: { id: string }) {
  const query = useQuery({
    queryKey: [...queryKeys.organization.branches(), id],
    queryFn: () => apiRequest<Schemas["BranchDto"]>(`/branches/${id}`),
  });

  if (query.isLoading) return <Skeleton className="h-72" />;
  if (query.error || !query.data) {
    return <Alert tone="danger">{errorMessage(query.error)}</Alert>;
  }

  return (
    <>
      <PageHeader
        title={query.data.name}
        description="Branch settings are managed by a platform administrator."
        actions={
          <Button variant="secondary">
            <Link href="/settings/branches">Back to branches</Link>
          </Button>
        }
      />
      <Alert tone="info">
        Organization managers can view branches but cannot add, update, or
        delete them.
      </Alert>
      <div className="mt-5">
        <DetailGrid data={query.data as unknown as Record<string, unknown>} />
      </div>
    </>
  );
}

export function BranchManagementNotice() {
  return (
    <>
      <PageHeader title="Branch creation is platform-managed" />
      <Alert tone="info">
        Only a platform administrator can add a branch to an organization.
      </Alert>
      <div className="mt-5">
        <Button variant="secondary">
          <Link href="/settings/branches">Back to branches</Link>
        </Button>
      </div>
    </>
  );
}
