import { identityClient } from '@/services/api-client'
import type { RoleCreateRequestModel, RoleModel, RoleUpdateRequestModel } from '@/features/identity/roles/types'

export async function fetchRoles(): Promise<RoleModel[]> {
  const { data } = await identityClient.get<RoleModel[]>('/api/role/all')
  return data
}

export async function createRole(payload: RoleCreateRequestModel): Promise<RoleModel> {
  const { data } = await identityClient.post<RoleModel>('/api/role', payload)
  return data
}

export async function updateRole(roleId: string, payload: RoleUpdateRequestModel): Promise<RoleModel> {
  const { data } = await identityClient.put<RoleModel>(`/api/role/${roleId}`, payload)
  return data
}

export async function deleteRole(roleId: string): Promise<void> {
  await identityClient.delete(`/api/role/${roleId}`)
}
