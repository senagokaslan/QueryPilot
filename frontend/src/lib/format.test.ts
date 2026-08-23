import { describe, expect, it } from "vitest";
import { toUtcExclusiveEnd, toUtcInclusiveEnd, toUtcStart } from "./format";

describe("date range conversion", () => {
  it("creates an exclusive analytics end boundary", () => {
    expect(new Date(toUtcExclusiveEnd("2026-08-23")).getTime() - new Date(toUtcStart("2026-08-23")).getTime()).toBe(86_400_000);
  });

  it("creates an inclusive order-list end boundary", () => {
    expect(new Date(toUtcInclusiveEnd("2026-08-23")).getTime()).toBeGreaterThan(new Date(toUtcStart("2026-08-23")).getTime());
  });
});
