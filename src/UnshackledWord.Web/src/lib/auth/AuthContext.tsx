import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import { authApi, type Me } from "./api";

export type AuthStatus = "loading" | "authenticated" | "anonymous";

export interface AuthState {
  status: AuthStatus;
  user: Me | null;
  /** Re-reads the session from the server (the cookie is not readable from script). */
  refresh: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  loginWithMagicLink: (token: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

/** Resolves the session once on load via GET /api/auth/me; the server stays the single source of truth. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");

  const refresh = useCallback(async () => {
    try {
      setUser(await authApi.me());
      setStatus("authenticated");
    } catch {
      setUser(null);
      setStatus("anonymous");
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo<AuthState>(
    () => ({
      status,
      user,
      refresh,
      login: async (email, password) => {
        await authApi.login(email, password);
        await refresh();
      },
      loginWithMagicLink: async (token) => {
        await authApi.magicLinkLogin(token);
        await refresh();
      },
      logout: async () => {
        await authApi.logout();
        setUser(null);
        setStatus("anonymous");
      },
    }),
    [status, user, refresh],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside <AuthProvider>");
  return context;
}
