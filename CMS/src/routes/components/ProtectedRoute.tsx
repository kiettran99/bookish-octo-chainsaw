import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { PropsWithChildren } from 'react'

import { CORE_ACCESS_ROLES } from '@/constants/roles'
import { STORAGE_KEYS } from '@/constants/storage'
import { FullPageSpinner } from '@/components/feedback/FullPageSpinner'
import { useAuth } from '@/hooks/useAuth'
import { writeToStorage } from '@/utils/localStorage'

interface ProtectedRouteProps {
  allowedRoles?: ReadonlyArray<string>
}

export function ProtectedRoute({ allowedRoles, children }: PropsWithChildren<ProtectedRouteProps>) {
  const { auth, isAuthorized } = useAuth()
  const location = useLocation()

  if (auth.status === 'checking' || auth.status === 'idle') {
    return <FullPageSpinner />
  }

  if (auth.status !== 'authenticated') {
    const target = `${location.pathname}${location.search}${location.hash}`
    writeToStorage(STORAGE_KEYS.authRedirect, target)
    return <Navigate to="/login" replace state={{ from: target }} />
  }

  const requiredRoles = allowedRoles ?? CORE_ACCESS_ROLES
  const requiresRoleCheck = requiredRoles.length > 0
  const hasRequiredRole = !requiresRoleCheck || isAuthorized(requiredRoles)

  if (!hasRequiredRole) {
    return <Navigate to="/" replace state={{ reason: 'Access denied' }} />
  }

  return children ? <>{children}</> : <Outlet />
}
