import axios, { type AxiosError, type AxiosResponse } from 'axios'

import type { ServiceResponse } from '@/types/service-response'

export async function unwrapServiceResponse<T>(
  promise: Promise<AxiosResponse<ServiceResponse<T>>>,
): Promise<T> {
  const { data } = await promise

  if (!data.isSuccess) {
    throw new Error(data.errorMessage ?? 'Request failed')
  }

  return data.data as T
}

export function unwrapErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ServiceResponse<unknown> | string>

    if (axiosError.response?.data) {
      const payload = axiosError.response.data

      if (typeof payload === 'string') {
        return payload
      }

      if (typeof payload === 'object' && payload !== null) {
        const servicePayload = payload as ServiceResponse<unknown>
        if (servicePayload.errorMessage) {
          return servicePayload.errorMessage
        }
      }
    }

    if (axiosError.message) {
      return axiosError.message
    }
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error occurred'
}
