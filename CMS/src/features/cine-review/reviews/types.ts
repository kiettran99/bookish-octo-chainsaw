export const REVIEW_STATUS = {
  Pending: 0,
  Released: 1,
  Deleted: 2,
} as const

export type ReviewStatus = (typeof REVIEW_STATUS)[keyof typeof REVIEW_STATUS]

export const REVIEW_STATUS_LABELS: Record<ReviewStatus, string> = {
  [REVIEW_STATUS.Pending]: 'Pending',
  [REVIEW_STATUS.Released]: 'Released',
  [REVIEW_STATUS.Deleted]: 'Deleted',
}

export const REVIEW_TYPE = {
  Tag: 0,
  Normal: 1,
} as const

export type ReviewType = (typeof REVIEW_TYPE)[keyof typeof REVIEW_TYPE]

export const REVIEW_TYPE_LABELS: Record<ReviewType, string> = {
  [REVIEW_TYPE.Tag]: 'Tag-based',
  [REVIEW_TYPE.Normal]: 'Standard',
}

export interface ReviewResponseModel {
  id: number
  userId: number
  userName?: string | null
  userAvatar?: string | null
  tmdbMovieId: number
  status: ReviewStatus
  communicationScore: number
  type: ReviewType
  descriptionTag?: unknown
  description?: string | null
  rating: number
  fairVotes: number
  unfairVotes: number
  createdOnUtc: string
  updatedOnUtc?: string | null
}

export interface ReviewListRequestModel {
  tmdbMovieId?: number
  userId?: number
  status?: ReviewStatus
  type?: ReviewType
  email?: string
  dateFrom?: string
  dateTo?: string
  page?: number
  pageSize?: number
}

export interface ReviewListResponse {
  items: ReviewResponseModel[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
