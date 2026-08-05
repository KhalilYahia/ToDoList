import { Card } from "@/components/ui/primitives";
import { titleFromCode } from "@/lib/utils";

export function DetailGrid({
  data,
  omit = [],
}: {
  data: Record<string, unknown>;
  omit?: string[];
}) {
  return (
    <Card>
      <dl className="grid gap-x-8 gap-y-5 sm:grid-cols-2 xl:grid-cols-3">
        {Object.entries(data)
          .filter(
            ([key, value]) =>
              !omit.includes(key) &&
              !Array.isArray(value) &&
              typeof value !== "object",
          )
          .map(([key, value]) => (
            <div key={key}>
              <dt className="text-ink-600 text-xs font-bold tracking-wide uppercase">
                {titleFromCode(key)}
              </dt>
              <dd className="mt-1 text-sm font-semibold break-words">
                {value === null || value === undefined || value === ""
                  ? "—"
                  : typeof value === "boolean"
                    ? value
                      ? "Yes"
                      : "No"
                    : String(value)}
              </dd>
            </div>
          ))}
      </dl>
    </Card>
  );
}
