import { describe, expect, it } from "vitest";
import { removeQueryString, sanitizeAttributes } from "./index.js";

describe("browser telemetry safety", () => {
  it("removes query strings and fragments", () => {
    expect(removeQueryString("/orders?token=secret#details")).toBe("/orders");
  });

  it("drops unapproved attributes", () => {
    expect(sanitizeAttributes({
      "user.email": "person@example.com",
      "telemetrybridge.feature.name": "orders"
    })).toEqual({ "telemetrybridge.feature.name": "orders" });
  });
});
