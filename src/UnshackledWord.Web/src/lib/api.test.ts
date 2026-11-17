import { describe, expect, it } from "vitest";
import "@/i18n";
import { ApiError, describeApiErrors } from "./api";

const failed = (body: unknown, status = 400) =>
  new ApiError(status, "failed", typeof body === "string" ? body : JSON.stringify(body));

describe("describeApiErrors", () => {
  it("lists every message of a validation body", () => {
    const error = failed({
      statusCode: 400,
      message: "One or more errors occurred!",
      errors: { generalErrors: ["a", "b"], links: ["c"] },
    });
    expect(describeApiErrors(error)).toEqual(["a", "b", "c"]);
  });

  it("falls back to the message, then to nothing", () => {
    expect(describeApiErrors(failed({ message: "Nicht gefunden" }, 404))).toEqual([
      "Nicht gefunden",
    ]);
    expect(describeApiErrors(failed({ errors: {} }))).toEqual([]);
    expect(describeApiErrors(failed("<html>bad gateway</html>", 502))).toEqual([]);
    expect(describeApiErrors(new ApiError(500, "x"))).toEqual([]);
    expect(describeApiErrors(new Error("network"))).toEqual([]);
    expect(describeApiErrors(undefined)).toEqual([]);
  });

  it("ignores empty and non-text entries", () => {
    expect(describeApiErrors(failed({ errors: { x: ["", "ok", 3, null] } }))).toEqual(["ok"]);
  });

  it("returns the rate-limit sentence for a 429", () => {
    const error = failed({ tier: "general", retryAfterSeconds: 12 }, 429);
    const messages = describeApiErrors(error);
    expect(messages).toHaveLength(1);
    expect(messages[0]).toMatch(/12/);
  });
});
