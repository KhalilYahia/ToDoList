"use client";

import { useQueries } from "@tanstack/react-query";

import { apiRequest } from "@/lib/api/client";
import type { PagedResponse, Schemas } from "@/lib/api/types";
import { queryKeys } from "@/lib/query/query-keys";

export function useReferenceData() {
  const [branches, departments, members, taskTemplates, orderTemplates] =
    useQueries({
      queries: [
        {
          queryKey: queryKeys.organization.branches({ pageSize: 200 }),
          queryFn: () =>
            apiRequest<PagedResponse<Schemas["BranchDto"]>>(
              "/branches?page=1&pageSize=200",
            ),
        },
        {
          queryKey: queryKeys.organization.departments({ pageSize: 200 }),
          queryFn: () =>
            apiRequest<PagedResponse<Schemas["DepartmentDto"]>>(
              "/departments?page=1&pageSize=200",
            ),
        },
        {
          queryKey: queryKeys.organization.members({ pageSize: 200 }),
          queryFn: () =>
            apiRequest<PagedResponse<Schemas["MemberDto"]>>(
              "/members?page=1&pageSize=200",
            ),
          retry: false,
        },
        {
          queryKey: queryKeys.taskTemplates.lists({ pageSize: 200 }),
          queryFn: () =>
            apiRequest<PagedResponse<Schemas["TaskTemplateDto"]>>(
              "/task-templates?page=1&pageSize=200",
            ),
        },
        {
          queryKey: queryKeys.orderTemplates.lists({ pageSize: 200 }),
          queryFn: () =>
            apiRequest<PagedResponse<Schemas["OrderTemplateDto"]>>(
              "/order-templates?page=1&pageSize=200",
            ),
        },
      ],
    });

  return {
    branches: branches.data?.items ?? [],
    departments: departments.data?.items ?? [],
    members: members.data?.items ?? [],
    taskTemplates: taskTemplates.data?.items ?? [],
    orderTemplates: orderTemplates.data?.items ?? [],
    isLoading: branches.isLoading || departments.isLoading,
  };
}
