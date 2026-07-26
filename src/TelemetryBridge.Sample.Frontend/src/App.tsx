import { useCallback, useEffect, useState } from "react";
import { traceAction } from "@telemetry-bridge/browser";

interface Order {
  id: string;
  channel: string;
  createdAt: string;
}

export default function App({ apiOrigin }: { apiOrigin: string }) {
  const [orders, setOrders] = useState<Order[]>([]);
  const [status, setStatus] = useState("Loading sample data…");

  const loadOrders = useCallback(async () => {
    const response = await fetch(`${apiOrigin}/api/orders`);
    if (!response.ok) throw new Error(`Order query failed (${response.status})`);
    setOrders(await response.json() as Order[]);
    setStatus("Telemetry is flowing.");
  }, [apiOrigin]);

  useEffect(() => {
    loadOrders().catch(() => setStatus("The API is not available yet."));
  }, [loadOrders]);

  async function submitOrder() {
    setStatus("Creating an instrumented order…");
    try {
      await traceAction("ui.order.submit", async () => {
        const response = await fetch(`${apiOrigin}/api/orders`, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ channel: "web" })
        });
        if (!response.ok) throw new Error(`Order creation failed (${response.status})`);
        await loadOrders();
      }, {
        "telemetrybridge.feature.name": "orders",
        "telemetrybridge.operation.type": "create"
      });
      setStatus("Order created. Open Grafana to follow its trace.");
    } catch {
      setStatus("Order creation failed. Check API and Collector health.");
    }
  }

  return (
    <main>
      <header>
        <span className="eyebrow">Vendor-neutral observability</span>
        <h1>TelemetryBridge</h1>
        <p>One click produces a browser span, an API span, structured logs, metrics, and a PostgreSQL dependency trace.</p>
        <button type="button" onClick={submitOrder}>Create traced order</button>
        <span className="status" role="status">{status}</span>
      </header>
      <section aria-labelledby="orders-title">
        <h2 id="orders-title">Recent sample orders</h2>
        {orders.length === 0
          ? <p className="empty">No orders yet.</p>
          : <ul>{orders.map(order => <li key={order.id}><strong>{order.channel}</strong><time>{new Date(order.createdAt).toLocaleString()}</time></li>)}</ul>}
      </section>
    </main>
  );
}
