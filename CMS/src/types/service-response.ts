export interface ServiceResponse<T> {
  readonly isSuccess: boolean
  readonly errorMessage?: string
  readonly errorKey?: unknown
  readonly data?: T
}
