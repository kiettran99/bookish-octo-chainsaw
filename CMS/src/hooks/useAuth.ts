import { useApplicationContext } from '@/contexts/ApplicationContext'

export function useAuth() {
  const {
    auth,
    loginWithResponse,
    logout,
    refreshProfile,
    isAuthorized,
  } = useApplicationContext()

  return {
    auth,
    loginWithResponse,
    logout,
    refreshProfile,
    isAuthorized,
  }
}
