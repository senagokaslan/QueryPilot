import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AppShell } from "./AppShell";
import { Pagination } from "./ui";

describe("Pagination", () => {
  it("moves to the next backend page", async () => {
    const onPage = vi.fn();
    render(<Pagination page={2} totalPages={4} totalCount={74} onPage={onPage} />);
    await userEvent.click(screen.getByRole("button", { name: "Sonraki sayfa" }));
    expect(onPage).toHaveBeenCalledWith(3);
  });

  it("disables previous navigation on the first page", () => {
    render(<Pagination page={1} totalPages={3} totalCount={60} onPage={() => undefined} />);
    expect(screen.getByRole("button", { name: "Önceki sayfa" })).toBeDisabled();
  });
});

describe("AppShell", () => {
  it("uses the product-specific navigation without a global API badge", () => {
    sessionStorage.setItem("qp-intro-seen", "1");
    render(<MemoryRouter><AppShell><div>İçerik</div></AppShell></MemoryRouter>);
    expect(screen.getByRole("link", { name: "Veriye Sor" })).toBeInTheDocument();
    expect(screen.queryByText("API bağlantısı aktif")).not.toBeInTheDocument();
    expect(screen.queryByText("Güvenli AI katmanı")).not.toBeInTheDocument();
  });
});
