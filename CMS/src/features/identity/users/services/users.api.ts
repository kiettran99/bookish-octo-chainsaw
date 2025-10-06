import { identityClient } from '@/services/api-client'
import { unwrapServiceResponse } from '@/utils/serviceResponse'
import type { UserPagingParams, UserPagingResponse, UserUpdateRequestModel, UserPagingModel } from '@/features/identity/users/types'

export async function fetchUsers(params: UserPagingParams = {}): Promise<UserPagingResponse> {
  return unwrapServiceResponse(
    identityClient.get('/api/user/paging', {
      params,
    }),
  )
}

export async function updateUser(userId: number, payload: UserUpdateRequestModel): Promise<boolean> {
  return unwrapServiceResponse(identityClient.put(`/api/user/${userId}`, payload))
}

export async function fetchPartners(): Promise<UserPagingModel[]> {
  return unwrapServiceResponse(identityClient.get('/api/user/partners/all'))
}
