import { CORE_ACCESS_ROLES } from '@/constants/roles'
import { useApplicationContext } from '@/contexts/ApplicationContext'

export function useAuth() {
  const {
    auth,
    loginWithResponse,
    logout,
    refreshProfile,
    isAuthorized,
  } = useApplicationContext()

  const canAccessCorePortal = isAuthorized(CORE_ACCESS_ROLES)

  return {
    auth,
    loginWithResponse,
    logout,
    refreshProfile,
    isAuthorized,
    canAccessCorePortal,
  }
}
