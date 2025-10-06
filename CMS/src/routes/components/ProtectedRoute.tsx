import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { PropsWithChildren } from 'react'

import { CORE_ACCESS_ROLES } from '@/constants/roles'
import { FullPageSpinner } from '@/components/feedback/FullPageSpinner'
import { useAuth } from '@/hooks/useAuth'

interface ProtectedRouteProps {
  allowedRoles?: ReadonlyArray<string>
}

export function ProtectedRoute({ allowedRoles, children }: PropsWithChildren<ProtectedRouteProps>) {
  const { auth, canAccessCorePortal, isAuthorized } = useAuth()
  const location = useLocation()

  if (auth.status === 'checking' || auth.status === 'idle') {
    return <FullPageSpinner />
  }

  const requiredRoles = allowedRoles ?? CORE_ACCESS_ROLES
  const isAllowed = isAuthorized(requiredRoles) && canAccessCorePortal

  if (auth.status !== 'authenticated' || !isAllowed) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children ? <>{children}</> : <Outlet />
}
