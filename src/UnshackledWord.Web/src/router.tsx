import { createBrowserRouter, Navigate } from "react-router";
import { AppShell } from "@/components/AppShell";
import { AboutPage } from "@/pages/AboutPage";
import { AdminInvitationsPage } from "@/pages/AdminInvitationsPage";
import { BiblePage } from "@/pages/BiblePage";
import { BulkReplacePage } from "@/pages/BulkReplacePage";
import { ChapterPage } from "@/pages/ChapterPage";
import { DataQualityPage } from "@/pages/DataQualityPage";
import { ForgotPasswordPage, MagicLinkRequestPage } from "@/pages/EmailRequestPage";
import { LemmaEditorPage } from "@/pages/LemmaEditorPage";
import { MagicLinkLoginPage } from "@/pages/MagicLinkLoginPage";
import { MaintainerPage } from "@/pages/MaintainerPage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { PasskeysPage } from "@/pages/PasskeysPage";
import { RegisterPage } from "@/pages/RegisterPage";
import { ResetPasswordPage } from "@/pages/ResetPasswordPage";
import { SignInPage } from "@/pages/SignInPage";
import { StrongsPage } from "@/pages/StrongsPage";
import { VerifyEmailPage } from "@/pages/VerifyEmailPage";

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/bible" replace /> },
      { path: "about", element: <AboutPage /> },
      { path: "bible", element: <BiblePage /> },
      { path: "bible/strongs/:strongs", element: <StrongsPage /> },
      { path: "bible/:bookId/:chapter", element: <ChapterPage /> },
      { path: "login", element: <SignInPage /> },
      { path: "register", element: <RegisterPage /> },
      { path: "forgot-password", element: <ForgotPasswordPage /> },
      { path: "reset-password", element: <ResetPasswordPage /> },
      { path: "magic-link", element: <MagicLinkRequestPage /> },
      { path: "magic-link-login", element: <MagicLinkLoginPage /> },
      { path: "verify-email", element: <VerifyEmailPage /> },
      { path: "account/passkeys", element: <PasskeysPage /> },
      { path: "edit/lemmas", element: <LemmaEditorPage />, handle: { layout: "fill" } },
      {
        path: "edit/lemmas/:bookId/:chapter",
        element: <LemmaEditorPage />,
        handle: { layout: "fill" },
      },
      { path: "edit/quality", element: <DataQualityPage /> },
      { path: "edit/maintainer", element: <MaintainerPage /> },
      { path: "edit/revision/replace", element: <BulkReplacePage /> },
      { path: "admin/invitations", element: <AdminInvitationsPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
