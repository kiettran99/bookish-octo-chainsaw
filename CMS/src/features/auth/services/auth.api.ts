import { identityClient } from '@/services/api-client'
import type { AuthenticateResponse } from '@/types/auth'
import { unwrapServiceResponse } from '@/utils/serviceResponse'

export interface ClientAuthenticatePayload {
  providerAccountId: string
  name: string
  email: string
  image?: string
  region?: string
  emailVerified?: boolean
  ipAddress?: string
  browserFingerprint?: string
}

export async function clientAuthenticate(
  payload: ClientAuthenticatePayload,
): Promise<AuthenticateResponse> {
  return unwrapServiceResponse(
    identityClient.post('/api/account/client-authenticate', payload),
  )
}

export async function fetchProfile(authToken?: string): Promise<AuthenticateResponse> {
  const config = authToken
    ? { headers: { Authorization: `Bearer ${authToken}` } }
    : undefined

  return unwrapServiceResponse(identityClient.get('/api/account/profile', config))
}
