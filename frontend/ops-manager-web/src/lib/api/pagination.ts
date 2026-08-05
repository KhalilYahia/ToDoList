import { apiRequest, type ApiRequestOptions } from "./client";
import type { PagedResponse } from "./types";

const maximumPageSize = 200;

export async function fetchAllPages<T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<T[]> {
  const items: T[] = [];
  let page = 1;

  while (true) {
    const separator = path.includes("?") ? "&" : "?";
    const response = await apiRequest<PagedResponse<T>>(
      `${path}${separator}page=${page}&pageSize=${maximumPageSize}`,
      options,
    );
    items.push(...response.items);

    if (
      response.items.length === 0 ||
      items.length >= Number(response.totalCount)
    ) {
      return items;
    }
    page += 1;
  }
}
