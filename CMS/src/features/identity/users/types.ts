import type { PagingCommonResponse } from '@/types/pagination'

export interface UserPagingModel {
  id: number
  fullName?: string | null
  email: string
  userName: string
  avatar?: string | null
  region?: string | null
  roles?: string | null
  isBanned: boolean
  isDeleted: boolean
  createdOnUtc: string
  updatedOnUtc?: string | null
}

export interface UserUpdateRequestModel {
  fullName: string
  roles?: string[] | null
  isBanned: boolean
  region?: string | null
}

export interface UserPagingParams {
  pageNumber?: number
  pageSize?: number
  searchTerm?: string
  isBanned?: boolean
  isDeleted?: boolean
  dateFrom?: string
  dateTo?: string
}

export type UserPagingResponse = PagingCommonResponse<UserPagingModel>
