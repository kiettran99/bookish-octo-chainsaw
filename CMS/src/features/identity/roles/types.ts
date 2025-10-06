export interface RoleModel {
  id: string
  name: string
  normalizedName: string
}

export interface RoleCreateRequestModel {
  name: string
}

export interface RoleUpdateRequestModel {
  name: string
}
