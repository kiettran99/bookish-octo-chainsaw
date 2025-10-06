import {
  Avatar,
  Box,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  Typography,
} from '@mui/material'

import type { ReviewResponseModel } from '@/features/cine-review/reviews/types'

interface ReviewDetailDialogProps {
  open: boolean
  review: ReviewResponseModel | null
  onClose: () => void
}

export function ReviewDetailDialog({ open, review, onClose }: ReviewDetailDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Review details</DialogTitle>
      <DialogContent dividers>
        {review ? (
          <Stack spacing={3}>
            <Stack direction="row" spacing={2} alignItems="center">
              <Avatar src={review.userAvatar ?? undefined} sx={{ width: 56, height: 56 }}>
                {review.userName?.[0]?.toUpperCase() ?? '?'}
              </Avatar>
              <Box>
                <Typography variant="h6" fontWeight={700}>
                  {review.userName ?? `User #${review.userId}`}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  TMDB #{review.tmdbMovieId}
                </Typography>
              </Box>
            </Stack>

            <Stack direction="row" spacing={2} flexWrap="wrap">
              <Chip label={`Rating ${review.rating}/10`} color="primary" />
              <Chip label={`Fair votes ${review.fairVotes}`} />
              <Chip label={`Unfair votes ${review.unfairVotes}`} />
              <Chip label={`Comm score ${review.communicationScore.toFixed(2)}`} />
            </Stack>

            <Box>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Description
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {review.description || 'No description provided.'}
              </Typography>
            </Box>

            <Box>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Tag payload
              </Typography>
              <Box
                sx={{
                  bgcolor: (theme) => theme.palette.action.hover,
                  px: 2,
                  py: 1.5,
                  borderRadius: 2,
                  fontFamily: 'monospace',
                  fontSize: 13,
                  maxHeight: 280,
                  overflow: 'auto',
                }}
              >
                <pre style={{ margin: 0 }}>
                  {JSON.stringify(review.descriptionTag, null, 2)}
                </pre>
              </Box>
            </Box>

            <Divider />

            <Stack direction="row" spacing={3} flexWrap="wrap">
              <Box>
                <Typography variant="subtitle2" color="text.secondary">
                  Created
                </Typography>
                <Typography variant="body2">
                  {review.createdOnUtc ? new Date(review.createdOnUtc).toLocaleString() : '—'}
                </Typography>
              </Box>
              <Box>
                <Typography variant="subtitle2" color="text.secondary">
                  Updated
                </Typography>
                <Typography variant="body2">
                  {review.updatedOnUtc ? new Date(review.updatedOnUtc).toLocaleString() : '—'}
                </Typography>
              </Box>
            </Stack>
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary">
            No review selected.
          </Typography>
        )}
      </DialogContent>
    </Dialog>
  )
}
