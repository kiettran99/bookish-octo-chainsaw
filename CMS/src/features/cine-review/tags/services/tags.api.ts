import { cineReviewClient } from '@/services/api-client'
import { unwrapServiceResponse } from '@/utils/serviceResponse'
import type {
  CreateTagRequestModel,
  TagFilterRequestModel,
  TagResponseModel,
  UpdateTagRequestModel,
} from '@/features/cine-review/tags/types'

export async function fetchTags(filter?: TagFilterRequestModel): Promise<TagResponseModel[]> {
  return unwrapServiceResponse(
    cineReviewClient.get('/api/tag', {
      params: filter,
    }),
  )
}

export async function createTag(payload: CreateTagRequestModel): Promise<TagResponseModel> {
  return unwrapServiceResponse(cineReviewClient.post('/api/tag', payload))
}

export async function updateTag(payload: UpdateTagRequestModel): Promise<TagResponseModel> {
  return unwrapServiceResponse(cineReviewClient.put(`/api/tag/${payload.id}`, payload))
}

export async function deleteTag(tagId: number): Promise<boolean> {
  return unwrapServiceResponse(cineReviewClient.delete(`/api/tag/${tagId}`))
}
