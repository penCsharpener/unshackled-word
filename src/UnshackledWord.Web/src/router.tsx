import { createBrowserRouter } from "react-router";
import { AppShell } from "@/components/AppShell";
import { BiblePage } from "@/pages/BiblePage";
import { ChapterPage } from "@/pages/ChapterPage";
import { HomePage } from "@/pages/HomePage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { StrongsPage } from "@/pages/StrongsPage";

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "bible", element: <BiblePage /> },
      { path: "bible/strongs/:strongs", element: <StrongsPage /> },
      { path: "bible/:bookId/:chapter", element: <ChapterPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
