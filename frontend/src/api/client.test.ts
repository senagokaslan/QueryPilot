import { describe, expect, it } from "vitest";
import { ApiError, errorMessage, toQuery } from "./client";

describe("API utilities", () => {
  it("omits empty query values and preserves backend enum casing", () => {
    expect(toQuery({ page: 2, search: "", metric: "Revenue", categoryId: undefined })).toBe("?page=2&metric=Revenue");
  });

  it("prioritizes backend validation messages", () => {
    const error = new ApiError({ title: "Validation failed", status: 400, errors: { name: ["Name is required."] } });
    expect(errorMessage(error)).toBe("Name is required.");
  });
});
