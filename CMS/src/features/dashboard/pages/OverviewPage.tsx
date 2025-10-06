import { Box, Paper, Stack, Typography } from '@mui/material'

import { PageContainer } from '@/components/layout/PageContainer'

const metricTiles = [
  {
    title: 'Active Users',
    value: '—',
    description: 'Hook up analytics to populate real metrics.',
  },
  {
    title: 'Pending Reviews',
    value: '—',
    description: 'Monitor moderation queue across all platforms.',
  },
  {
    title: 'Active Tags',
    value: '—',
    description: 'Ensure discovery metadata stays fresh.',
  },
]

export function OverviewPage() {
  return (
    <PageContainer
      title="Control Center"
      subtitle="High-level insight into identity and content operations."
    >
      <Box
        sx={{
          display: 'grid',
          gap: 3,
          gridTemplateColumns: {
            xs: '1fr',
            sm: 'repeat(2, minmax(0, 1fr))',
            lg: 'repeat(3, minmax(0, 1fr))',
          },
        }}
      >
        {metricTiles.map((tile) => (
          <Paper
            key={tile.title}
            sx={{
              borderRadius: 4,
              p: 3,
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
            }}
          >
            <Stack spacing={1.5}>
              <Typography variant="overline" color="text.secondary">
                {tile.title}
              </Typography>
              <Typography variant="h4" fontWeight={700}>
                {tile.value}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {tile.description}
              </Typography>
            </Stack>
          </Paper>
        ))}
      </Box>
    </PageContainer>
  )
}
