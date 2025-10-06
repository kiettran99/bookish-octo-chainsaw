export const TAG_CATEGORY = {
  Content: 0,
  Acting: 1,
  AudioVisual: 2,
  TheaterExperience: 3,
} as const

export type TagCategory = (typeof TAG_CATEGORY)[keyof typeof TAG_CATEGORY]

export const TAG_CATEGORY_LABELS: Record<TagCategory, string> = {
  [TAG_CATEGORY.Content]: 'Content',
  [TAG_CATEGORY.Acting]: 'Acting',
  [TAG_CATEGORY.AudioVisual]: 'Audio / Visual',
  [TAG_CATEGORY.TheaterExperience]: 'Theater Experience',
}

export interface TagResponseModel {
  id: number
  name: string
  description?: string | null
  category: TagCategory
  categoryName: string
  isActive: boolean
  displayOrder: number
  createdOnUtc: string
  updatedOnUtc?: string | null
}

export interface CreateTagRequestModel {
  name: string
  description?: string | null
  category: TagCategory
  displayOrder: number
}

export interface UpdateTagRequestModel {
  id: number
  name: string
  description?: string | null
  category: TagCategory
  isActive: boolean
  displayOrder: number
}

export interface TagFilterRequestModel {
  category?: TagCategory
  isActive?: boolean
}
