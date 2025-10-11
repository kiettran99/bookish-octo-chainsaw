import { cineReviewClient } from '@/services/api-client'
import { unwrapServiceResponse } from '@/utils/serviceResponse'
import type { ReviewListRequestModel, ReviewListResponse } from '@/features/cine-review/reviews/types'

export async function fetchReviews(params: ReviewListRequestModel): Promise<ReviewListResponse> {
  return unwrapServiceResponse(
    cineReviewClient.get('/api/review', {
      params,
    }),
  )
}

export async function approveReview(reviewId: number): Promise<boolean> {
  return unwrapServiceResponse(cineReviewClient.post(`/api/review/${reviewId}/approve`))
}

export async function deleteReview(reviewId: number, rejectReason?: string): Promise<boolean> {
  return unwrapServiceResponse(
    cineReviewClient.delete(`/api/review/${reviewId}`, {
      params: rejectReason ? { rejectReason } : undefined,
    }),
  )
}

export async function updateReview(data: {
  reviewId: number
  type: number
  rating: number
  description?: string
  descriptionTag?: unknown
  status?: number
  rejectReason?: string
}): Promise<unknown> {
  return unwrapServiceResponse(cineReviewClient.put('/api/review', data))
}

export async function recalculateReviewScore(reviewId: number): Promise<boolean> {
  return unwrapServiceResponse(cineReviewClient.post(`/api/review/${reviewId}/recalculate-score`))
}
