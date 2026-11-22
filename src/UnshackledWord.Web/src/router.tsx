import { createBrowserRouter, Navigate } from "react-router";
import { AppShell } from "@/components/AppShell";
import { AboutPage } from "@/pages/AboutPage";
import { AdminInvitationsPage } from "@/pages/AdminInvitationsPage";
import { AdminLogPage } from "@/pages/AdminLogPage";
import { AdminUserCreatePage } from "@/pages/AdminUserCreatePage";
import { AdminUserDetailPage } from "@/pages/AdminUserDetailPage";
import { AdminUsersPage } from "@/pages/AdminUsersPage";
import { AgentProposalsPage } from "@/pages/agent/AgentProposalsPage";
import { BiblePage } from "@/pages/BiblePage";
import { BulkReplacePage } from "@/pages/BulkReplacePage";
import { ChapterPage } from "@/pages/ChapterPage";
import { DataQualityPage } from "@/pages/DataQualityPage";
import { ForgotPasswordPage, MagicLinkRequestPage } from "@/pages/EmailRequestPage";
import { LearnPage } from "@/pages/LearnPage";
import { LemmaEditorPage } from "@/pages/LemmaEditorPage";
import { MagicLinkLoginPage } from "@/pages/MagicLinkLoginPage";
import { MaintainerPage } from "@/pages/MaintainerPage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { PasskeysPage } from "@/pages/PasskeysPage";
import { PrivacyPage } from "@/pages/PrivacyPage";
import { RegisterPage } from "@/pages/RegisterPage";
import { ResetPasswordPage } from "@/pages/ResetPasswordPage";
import { SeedImportsPage } from "@/pages/SeedImportsPage";
import { SignInPage } from "@/pages/SignInPage";
import { StrongsPage } from "@/pages/StrongsPage";
import { StudySessionPage } from "@/pages/StudySessionPage";
import { VerifyEmailPage } from "@/pages/VerifyEmailPage";
import { VocabDetailPage } from "@/pages/VocabDetailPage";
import { VocabEditorListPage } from "@/pages/VocabEditorListPage";
import { VocabEditorPage } from "@/pages/VocabEditorPage";
import { VocabListPage } from "@/pages/VocabListPage";

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/bible" replace /> },
      { path: "about", element: <AboutPage /> },
      { path: "datenschutz", element: <PrivacyPage /> },
      { path: "privacy", element: <PrivacyPage /> },
      { path: "bible", element: <BiblePage /> },
      { path: "bible/strongs/:strongs", element: <StrongsPage /> },
      { path: "bible/:bookId/:chapter", element: <ChapterPage /> },
      { path: "vocab", element: <VocabListPage /> },
      { path: "vocab/:id", element: <VocabDetailPage /> },
      { path: "learn", element: <LearnPage /> },
      { path: "learn/:course", element: <StudySessionPage /> },
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
      { path: "edit/vocab", element: <VocabEditorListPage /> },
      { path: "edit/vocab/:id", element: <VocabEditorPage /> },
      { path: "edit/imports", element: <SeedImportsPage /> },
      { path: "edit/quality", element: <DataQualityPage /> },
      { path: "edit/maintainer", element: <MaintainerPage /> },
      { path: "edit/proposals/:runId?/:proposalId?", element: <AgentProposalsPage /> },
      { path: "edit/revision/replace", element: <BulkReplacePage /> },
      { path: "admin/users", element: <AdminUsersPage /> },
      { path: "admin/users/new", element: <AdminUserCreatePage /> },
      { path: "admin/users/:id", element: <AdminUserDetailPage /> },
      { path: "admin/invitations", element: <AdminInvitationsPage /> },
      { path: "admin/log", element: <AdminLogPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
