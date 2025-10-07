/* eslint-disable react-refresh/only-export-components */
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { PropsWithChildren } from 'react'
import type { PaletteMode } from '@mui/material'
import { useNavigate } from 'react-router-dom'

import { STORAGE_KEYS } from '@/constants/storage'
import type { CoreAccessRole } from '@/constants/roles'
import type { AuthState, AuthenticateResponse } from '@/types/auth'
import type { PlatformDefinition } from '@/types/platform'
import { readFromStorage, writeToStorage } from '@/utils/localStorage'
import { configureApiClients, updateApiBaseUrls } from '@/services/api-client'
import { fetchProfile } from '@/features/auth/services/auth.api'
import { unwrapErrorMessage } from '@/utils/serviceResponse'
import { ThemedContainer } from '@/theme'

export interface StoredAuthPayload {
  token: string
  user: AuthenticateResponse | null
}

const initialAuthState: AuthState = {
  status: 'idle',
  token: null,
  user: null,
  roles: [],
}

const envIdentityUrl = import.meta.env.VITE_IDENTITY_API_BASE_URL ?? 'http://localhost:5123'
const envCineReviewUrl = import.meta.env.VITE_CINEREVIEW_API_BASE_URL ?? 'http://localhost:5216'

const DEFAULT_PLATFORMS: PlatformDefinition[] = [
  {
    id: 'cine-review',
    label: 'CineReview',
    description: 'Core CineReview administration platform',
    apis: {
      identity: envIdentityUrl,
      cineReview: envCineReviewUrl,
    },
    isPrimary: true,
  },
]

interface ApplicationContextValue {
  themeMode: PaletteMode
  setThemeMode: (mode: PaletteMode) => void
  toggleThemeMode: () => void

  auth: AuthState
  loginWithResponse: (payload: AuthenticateResponse) => void
  logout: () => void
  refreshProfile: (tokenOverride?: string) => Promise<void>
  isAuthorized: (allowedRoles?: ReadonlyArray<CoreAccessRole | string>) => boolean

  platforms: PlatformDefinition[]
  currentPlatform: PlatformDefinition
  setPlatform: (platformId: string) => void
}

const ApplicationContext = createContext<ApplicationContextValue | undefined>(undefined)

function deriveStoredAuth(): AuthState {
  const stored = readFromStorage<StoredAuthPayload>(STORAGE_KEYS.auth)
  if (stored?.token) {
    const derivedRoles = stored.user?.roles ?? []
    return {
      status: 'checking',
      token: stored.token,
      user: stored.user,
      roles: derivedRoles,
    }
  }

  return { ...initialAuthState, status: 'unauthenticated' }
}

function deriveInitialTheme(): PaletteMode {
  const stored = readFromStorage<PaletteMode>(STORAGE_KEYS.theme)
  if (stored === 'dark' || stored === 'light') {
    return stored
  }

  if (typeof window !== 'undefined' && window.matchMedia) {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  }

  return 'light'
}

function deriveInitialPlatform(): PlatformDefinition {
  const storedId = readFromStorage<string>(STORAGE_KEYS.platform)
  if (storedId) {
    const matched = DEFAULT_PLATFORMS.find((platform) => platform.id === storedId)
    if (matched) {
      return matched
    }
  }

  return DEFAULT_PLATFORMS[0]
}

export function ApplicationProvider({ children }: PropsWithChildren) {
  const [themeMode, setThemeModeState] = useState<PaletteMode>(deriveInitialTheme)
  const [auth, setAuth] = useState<AuthState>(deriveStoredAuth)
  const [currentPlatform, setCurrentPlatform] = useState<PlatformDefinition>(deriveInitialPlatform)
  const navigate = useNavigate()
  const hasAttemptedInitialHydration = useRef(false)

  useEffect(() => {
    configureApiClients({
      getToken: () => auth.token,
      onUnauthorized: () => {
        hasAttemptedInitialHydration.current = false
        setAuth({ ...initialAuthState, status: 'unauthenticated' })
        writeToStorage(STORAGE_KEYS.auth, null)
        navigate('/login', { replace: true })
      },
    })
  }, [auth.token, navigate])

  useEffect(() => {
    updateApiBaseUrls({
      identity: currentPlatform.apis.identity,
      cineReview: currentPlatform.apis.cineReview,
    })
    writeToStorage(STORAGE_KEYS.platform, currentPlatform.id)
  }, [currentPlatform])

  useEffect(() => {
    writeToStorage(STORAGE_KEYS.theme, themeMode)
  }, [themeMode])

  const hydrateProfile = useCallback(
    async (token: string) => {
      setAuth((prev) => {
        const isSameToken = prev.token === token
        return {
          status: 'checking',
          token,
          user: isSameToken ? prev.user : null,
          roles: isSameToken ? prev.roles : [],
        }
      })

      try {
        const profile = await fetchProfile(token)
        setAuth({
          status: 'authenticated',
          token,
          user: profile,
          roles: profile.roles ?? [],
        })
        writeToStorage(STORAGE_KEYS.auth, { token, user: profile })
        return profile
      } catch (error) {
        setAuth({ ...initialAuthState, status: 'unauthenticated' })
        writeToStorage(STORAGE_KEYS.auth, null)
        throw error
      }
    },
    [],
  )

  const refreshProfile = useCallback(
    async (tokenOverride?: string) => {
      const token = tokenOverride ?? auth.token
      if (!token) {
        throw new Error('Cannot refresh profile without authentication token')
      }

      hasAttemptedInitialHydration.current = true
      await hydrateProfile(token)
    },
    [auth.token, hydrateProfile],
  )

  useEffect(() => {
    if (!auth.token) {
      hasAttemptedInitialHydration.current = false
      if (auth.status !== 'unauthenticated') {
        setAuth({ ...initialAuthState, status: 'unauthenticated' })
        writeToStorage(STORAGE_KEYS.auth, null)
      }
      return
    }

    if (auth.status !== 'checking') {
      return
    }

    if (hasAttemptedInitialHydration.current) {
      return
    }

    hasAttemptedInitialHydration.current = true

    void hydrateProfile(auth.token).catch((error) => {
      console.warn('Failed to refresh session', error)
      navigate('/login', { replace: true, state: { reason: unwrapErrorMessage(error) } })
    })
  }, [auth.status, auth.token, hydrateProfile, navigate])

  const setThemeMode = useCallback((mode: PaletteMode) => {
    setThemeModeState(mode)
  }, [])

  const toggleThemeMode = useCallback(() => {
    setThemeModeState((prev) => (prev === 'light' ? 'dark' : 'light'))
  }, [])

  const logout = useCallback(() => {
    hasAttemptedInitialHydration.current = false
    setAuth({ ...initialAuthState, status: 'unauthenticated' })
    writeToStorage(STORAGE_KEYS.auth, null)
    navigate('/login', { replace: true })
  }, [navigate])

  const loginWithResponse = useCallback((payload: AuthenticateResponse) => {
    if (!payload.jwtToken) {
      throw new Error('Missing JWT token in authentication response')
    }

    const nextAuth: AuthState = {
      status: 'authenticated',
      token: payload.jwtToken,
      user: payload,
      roles: payload.roles ?? [],
    }

    setAuth(nextAuth)
    writeToStorage(STORAGE_KEYS.auth, { token: payload.jwtToken, user: payload })
  }, [])

  const setPlatform = useCallback((platformId: string) => {
    const next = DEFAULT_PLATFORMS.find((platform) => platform.id === platformId)
    if (!next) {
      console.warn(`Unknown platform id ${platformId}`)
      return
    }

    setCurrentPlatform(next)
  }, [])

  const isAuthorized = useCallback(
    (allowedRoles?: ReadonlyArray<CoreAccessRole | string>) => {
      if (!allowedRoles || allowedRoles.length === 0) {
        return auth.status === 'authenticated'
      }

      return allowedRoles.some((role) => auth.roles.includes(role))
    },
    [auth.roles, auth.status],
  )

  const contextValue = useMemo<ApplicationContextValue>(() => {
    return {
      themeMode,
      setThemeMode,
      toggleThemeMode,
      auth,
      loginWithResponse,
      logout,
      refreshProfile,
      isAuthorized,
      platforms: DEFAULT_PLATFORMS,
      currentPlatform,
      setPlatform,
    }
  }, [themeMode, setThemeMode, toggleThemeMode, auth, loginWithResponse, logout, refreshProfile, isAuthorized, currentPlatform, setPlatform])

  return (
    <ApplicationContext.Provider value={contextValue}>
      <ThemedContainer mode={themeMode}>{children}</ThemedContainer>
    </ApplicationContext.Provider>
  )
}

export function useApplicationContext() {
  const ctx = useContext(ApplicationContext)
  if (!ctx) {
    throw new Error('useApplicationContext must be used within ApplicationProvider')
  }

  return ctx
}
