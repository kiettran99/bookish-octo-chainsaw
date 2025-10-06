export interface AuthenticateResponse {
  readonly id?: number | null
  readonly fullName?: string | null
  readonly userName?: string | null
  readonly avatar?: string | null
  readonly email?: string | null
  readonly jwtToken?: string | null
  readonly roles: string[]
  readonly expriedRoleDate?: string | null
  readonly createdOnUtc?: string | null
}

export interface AuthState {
  readonly status: 'idle' | 'checking' | 'authenticated' | 'unauthenticated' | 'error'
  readonly token: string | null
  readonly user: AuthenticateResponse | null
  readonly roles: string[]
  readonly error?: string | null
}
