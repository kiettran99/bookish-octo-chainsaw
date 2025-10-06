export interface PagingCommonResponse<T> {
  readonly rowNum?: number
  readonly data?: T[]
}

export interface PagingParams {
  pageNumber?: number
  pageSize?: number
  searchTerm?: string
  sortColumn?: string
  sortDescending?: boolean
}
