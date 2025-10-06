export const CORE_ACCESS_ROLES = ['Administrator', 'Partner'] as const

export type CoreAccessRole = (typeof CORE_ACCESS_ROLES)[number]

export const ROLE_LABELS: Record<string, string> = {
  Administrator: 'Administrator',
  Partner: 'Partner',
  'VIP User': 'VIP User',
  User: 'User',
}
