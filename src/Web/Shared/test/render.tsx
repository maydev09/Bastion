import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import { vi } from "vitest";

// path may carry the history state a page reads, e.g. the list an offer card was opened from.
export function renderWithProviders(
  ui: ReactNode,
  path: string | { pathname: string; state: unknown } = "/",
) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}

// Answers requests by path; the returned list collects every requested URL, query included.
export function stubApi(responses: Record<string, { status: number; body: unknown }>) {
  const requests: URL[] = [];
  vi.stubGlobal("fetch", async (request: Request) => {
    const url = new URL(request.url);
    requests.push(url);
    const response = responses[url.pathname];
    return response
      ? new Response(JSON.stringify(response.body), {
          status: response.status,
          headers: { "Content-Type": "application/json" },
        })
      : new Response(null, { status: 404 });
  });
  return requests;
}
