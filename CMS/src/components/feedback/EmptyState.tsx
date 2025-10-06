import { Box, Typography } from '@mui/material'
import type { ReactNode } from 'react'

interface EmptyStateProps {
  title: string
  description?: string
  icon?: ReactNode
}

export function EmptyState({ title, description, icon }: EmptyStateProps) {
  return (
    <Box
      sx={{
        borderRadius: 4,
        border: '1px dashed',
        borderColor: (theme) => theme.palette.divider,
        px: 6,
        py: 9,
        textAlign: 'center',
        color: 'text.secondary',
      }}
    >
      {icon ? <Box sx={{ mb: 2 }}>{icon}</Box> : null}
      <Typography variant="h6" gutterBottom>
        {title}
      </Typography>
      {description ? (
        <Typography variant="body2" color="text.secondary">
          {description}
        </Typography>
      ) : null}
    </Box>
  )
}
