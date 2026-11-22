import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { CourseCard, type CourseCardProps } from "./CourseCard";

const base = {
  title: "Neues Testament (Griechisch)",
  subtitle: "Griechisch · 5.407 Wörter nach Häufigkeit",
  coverageText: "NT-Textes",
  to: "/learn/nt-greek",
  listTo: "/vocab?track=G",
};

function renderCard(props: Partial<CourseCardProps>) {
  return render(
    <MemoryRouter>
      <CourseCard {...base} signedIn={false} {...props} />
    </MemoryRouter>,
  );
}

describe("CourseCard", () => {
  it("gives a signed-in learner the start link", () => {
    renderCard({ signedIn: true });
    expect(screen.getByRole("link", { name: "Beginnen" })).toHaveAttribute(
      "href",
      "/learn/nt-greek",
    );
    expect(screen.queryByRole("link", { name: "Wortliste ansehen" })).toBeNull();
  });

  it("gives an anonymous visitor the word list instead", () => {
    renderCard({ signedIn: false });
    expect(screen.getByRole("link", { name: "Wortliste ansehen" })).toHaveAttribute(
      "href",
      "/vocab?track=G",
    );
    expect(screen.queryByRole("link", { name: "Beginnen" })).toBeNull();
  });

  it("shows the learner's numbers when they are given", () => {
    renderCard({
      signedIn: true,
      stats: { known: 142, due: 18, new: 5270, coverage: 62.4 },
    });
    expect(screen.getByText("Bekannt")).toBeInTheDocument();
    expect(screen.getByText("5.270")).toBeInTheDocument();
    expect(screen.getByText("62,4 % des NT-Textes kennst du schon.")).toBeInTheDocument();
  });

  it("shows no numbers without stats", () => {
    renderCard({ signedIn: true });
    expect(screen.queryByText("Bekannt")).toBeNull();
  });
});
