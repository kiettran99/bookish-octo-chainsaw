import { useCallback, useEffect, useRef } from 'react'
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
  const replaceWithSanitizedUrl = useCallback(() => {
    const sanitizedSearch = withoutToken(location.search)
    if (sanitizedSearch === location.search) {
      return
    }

    navigate(
      {
        pathname: location.pathname,
        search: sanitizedSearch,
        hash: location.hash || undefined,
      },
      { replace: true, state: { cleanedToken: true } },
    )
  }, [location.hash, location.pathname, location.search, navigate])

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    const sanitizedSearch = withoutToken(location.search)
    const token = params.get('token')
    const hasTokenParam = params.has('token')

    if (!hasTokenParam) {
      handledToken.current = null
      return
    }

    if (!token) {
      handledToken.current = null
      replaceWithSanitizedUrl()
      return
    }

    if (handledToken.current === token) {
      replaceWithSanitizedUrl()
      return
    }

    handledToken.current = token
    replaceWithSanitizedUrl()
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

        navigate(
          {
            pathname: '/login',
            search: sanitizedSearch,
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

      // Sau khi xử lý thành công, luôn xóa token khỏi URL
      const targetPath = location.pathname === '/login' ? '/' : location.pathname

      navigate(
        {
          pathname: targetPath,
          search: sanitizedSearch,
        },
        { replace: true, state: { cleanedToken: true } },
      )
    })()

    return () => {
      active = false
    }
  }, [location.pathname, location.search, loginWithResponse, refreshProfile, navigate, replaceWithSanitizedUrl])

  return null
}

function withoutToken(search: string): string | undefined {
  if (!search) {
    return undefined
  }

  const params = new URLSearchParams(search)
  if (!params.has('token')) {
    const current = params.toString()
    return current ? `?${current}` : undefined
  }

  params.delete('token')
  const cleaned = params.toString()
  return cleaned ? `?${cleaned}` : undefined
}
