import { createBrowserRouter } from "react-router";
import { AppShell } from "@/components/AppShell";
import { AdminInvitationsPage } from "@/pages/AdminInvitationsPage";
import { BiblePage } from "@/pages/BiblePage";
import { ChapterPage } from "@/pages/ChapterPage";
import { ForgotPasswordPage, MagicLinkRequestPage } from "@/pages/EmailRequestPage";
import { HomePage } from "@/pages/HomePage";
import { MagicLinkLoginPage } from "@/pages/MagicLinkLoginPage";
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
      { index: true, element: <HomePage /> },
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
      { path: "admin/invitations", element: <AdminInvitationsPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
