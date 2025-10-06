import { useEffect, useRef } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { enqueueSnackbar } from 'notistack'

import { ApplicationProvider } from '@/contexts/ApplicationContext'
import { STORAGE_KEYS } from '@/constants/storage'
import { useAuth } from '@/hooks/useAuth'
import { readFromStorage, writeToStorage } from '@/utils/localStorage'
import { unwrapErrorMessage } from '@/utils/serviceResponse'

export function AppRoot() {
  return (
    <ApplicationProvider>
      <AppShell />
    </ApplicationProvider>
  )
}

function AppShell() {
  return (
    <>
      <AuthCallbackHandler />
      <Outlet />
    </>
  )
}

function AuthCallbackHandler() {
  const location = useLocation()
  const navigate = useNavigate()
  const { loginWithResponse, refreshProfile } = useAuth()
  const handledToken = useRef<string | null>(null)

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    const token = params.get('token')

    if (!token) {
      handledToken.current = null
      return
    }

    if (handledToken.current === token) {
      return
    }

    handledToken.current = token
    let active = true

    ;(async () => {
      const redirectTarget = readFromStorage<string>(STORAGE_KEYS.authRedirect)

      try {
        loginWithResponse({ jwtToken: token, roles: [] })
        await refreshProfile(token)
        if (!active) {
          return
        }
        enqueueSnackbar('Signed in successfully', { variant: 'success' })
      } catch (error) {
        if (!active) {
          return
        }

        const reason = unwrapErrorMessage(error)
        enqueueSnackbar(reason, { variant: 'error' })
        writeToStorage(STORAGE_KEYS.authRedirect, null)
        handledToken.current = null

        const cleaned = new URLSearchParams(location.search)
        cleaned.delete('token')
        const cleanedSearch = cleaned.toString()
        navigate(
          {
            pathname: '/login',
            search: cleanedSearch ? `?${cleanedSearch}` : undefined,
          },
          { replace: true, state: { reason } },
        )
        return
      }

      writeToStorage(STORAGE_KEYS.authRedirect, null)

      if (!active) {
        return
      }

      if (redirectTarget) {
        navigate(redirectTarget, { replace: true })
        return
      }

      const cleaned = new URLSearchParams(location.search)
      cleaned.delete('token')
      const cleanedSearch = cleaned.toString()
      const targetPath = location.pathname === '/login' ? '/' : location.pathname

      navigate(
        {
          pathname: targetPath,
          search: cleanedSearch ? `?${cleanedSearch}` : undefined,
        },
        { replace: true },
      )
    })()

    return () => {
      active = false
    }
  }, [location.pathname, location.search, loginWithResponse, refreshProfile, navigate])

  return null
}
