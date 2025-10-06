import { Box, Container, Stack, Typography } from '@mui/material'
import type { PropsWithChildren, ReactNode } from 'react'

interface PageContainerProps extends PropsWithChildren {
  title: ReactNode
  subtitle?: ReactNode
  action?: ReactNode
}

export function PageContainer({ title, subtitle, action, children }: PageContainerProps) {
  return (
    <Container maxWidth="xl" sx={{ mb: 6 }}>
      <Stack direction="row" justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={3} mb={4} flexWrap="wrap">
        <Box>
          {typeof title === 'string' ? (
            <Typography variant="h4" fontWeight={700} gutterBottom>
              {title}
            </Typography>
          ) : (
            title
          )}
          {subtitle ? (
            <Typography variant="body1" color="text.secondary">
              {subtitle}
            </Typography>
          ) : null}
        </Box>
        {action ? <Box>{action}</Box> : null}
      </Stack>
      <Box>{children}</Box>
    </Container>
  )
}
