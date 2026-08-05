import { fetchAllPages } from "./pagination";

describe("paginated API aggregation", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("loads every page when a manager needs complete reference data", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            items: [{ id: "one" }, { id: "two" }],
            page: 1,
            pageSize: 200,
            totalCount: 3,
          }),
          {
            status: 200,
            headers: { "Content-Type": "application/json" },
          },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            items: [{ id: "three" }],
            page: 2,
            pageSize: 200,
            totalCount: 3,
          }),
          {
            status: 200,
            headers: { "Content-Type": "application/json" },
          },
        ),
      );
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchAllPages<{ id: string }>("/members")).resolves.toEqual([
      { id: "one" },
      { id: "two" },
      { id: "three" },
    ]);
    expect(fetchMock.mock.calls[0]?.[0]).toContain(
      "/members?page=1&pageSize=200",
    );
    expect(fetchMock.mock.calls[1]?.[0]).toContain(
      "/members?page=2&pageSize=200",
    );
  });
});
