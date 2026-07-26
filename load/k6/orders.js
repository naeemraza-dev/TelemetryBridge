import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  scenarios: {
    steady: {
      executor: "constant-arrival-rate",
      rate: Number(__ENV.REQUESTS_PER_SECOND || 20),
      timeUnit: "1s",
      duration: __ENV.DURATION || "2m",
      preAllocatedVUs: 20,
      maxVUs: 100
    }
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"]
  }
};

const origin = __ENV.BASE_URL || "http://host.docker.internal:8080";

export default function () {
  const response = http.post(
    `${origin}/api/orders`,
    JSON.stringify({ channel: "web" }),
    { headers: { "Content-Type": "application/json" } }
  );
  check(response, {
    "order created": (result) => result.status === 201,
    "correlation returned": (result) => Boolean(result.headers["X-Correlation-Id"])
  });
  sleep(0.05);
}
