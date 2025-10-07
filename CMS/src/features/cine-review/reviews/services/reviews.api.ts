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

export async function recalculateReviewScore(reviewId: number): Promise<boolean> {
  return unwrapServiceResponse(cineReviewClient.post(`/api/review/${reviewId}/recalculate-score`))
}
