export interface PlatformDefinition {
  readonly id: string
  readonly label: string
  readonly description?: string
  readonly apis: {
    readonly identity: string
    readonly cineReview: string
  }
  readonly isPrimary?: boolean
}
