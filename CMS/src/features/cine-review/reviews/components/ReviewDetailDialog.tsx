import {
  Avatar,
  Box,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  LinearProgress,
  Stack,
  Typography,
} from '@mui/material'
import { useQuery } from '@tanstack/react-query'

import type { ReviewResponseModel } from '@/features/cine-review/reviews/types'
import { REVIEW_TYPE } from '@/features/cine-review/reviews/types'
import { fetchTags } from '@/features/cine-review/tags/services/tags.api'
import type { TagResponseModel } from '@/features/cine-review/tags/types'

interface ReviewDetailDialogProps {
  open: boolean
  review: ReviewResponseModel | null
  onClose: () => void
}

interface TagRatingItem {
  tagId: number
  rating: number
}

export function ReviewDetailDialog({ open, review, onClose }: ReviewDetailDialogProps) {
  const tagsQuery = useQuery({
    queryKey: ['tags'],
    queryFn: () => fetchTags(),
    enabled: open,
    staleTime: 5 * 60 * 1000,
  })

  const getTagById = (tagId: number): TagResponseModel | undefined => {
    return tagsQuery.data?.find((tag) => tag.id === tagId)
  }

  const parseTagRatings = (): TagRatingItem[] => {
    if (!review?.descriptionTag) return []
    
    try {
      const data = Array.isArray(review.descriptionTag) 
        ? review.descriptionTag 
        : JSON.parse(JSON.stringify(review.descriptionTag))
      
      if (Array.isArray(data)) {
        return data.map((item: any) => ({
          tagId: item.tagId || item.TagId,
          rating: item.rating || item.Rating,
        }))
      }
      return []
    } catch {
      return []
    }
  }

  const tagRatings = review?.type === REVIEW_TYPE.Tag ? parseTagRatings() : []

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
              <Chip 
                label={review.type === REVIEW_TYPE.Tag ? 'Tag-based review' : 'Standard review'} 
                color={review.type === REVIEW_TYPE.Tag ? 'info' : 'default'}
              />
              <Chip label={`Rating ${review.rating}/10`} color="primary" />
              <Chip label={`Comm score ${review.communicationScore.toFixed(2)}`} />
            </Stack>

            {review.type === REVIEW_TYPE.Normal && (
              <Box>
                <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                  Description
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {review.description || 'No description provided.'}
                </Typography>
              </Box>
            )}

            {review.type === REVIEW_TYPE.Tag && tagRatings.length > 0 && (
              <Box>
                <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                  Tag Ratings
                </Typography>
                <Stack spacing={2}>
                  {tagRatings.map((item) => {
                    const tag = getTagById(item.tagId)
                    return (
                      <Box key={item.tagId}>
                        <Stack direction="row" justifyContent="space-between" alignItems="center" mb={0.5}>
                          <Typography variant="body2" fontWeight={500}>
                            {tag?.name ?? `Tag #${item.tagId}`}
                            {tag && (
                              <Chip 
                                label={tag.categoryName} 
                                size="small" 
                                sx={{ ml: 1 }}
                                variant="outlined"
                              />
                            )}
                          </Typography>
                          <Typography variant="body2" color="primary" fontWeight={600}>
                            {item.rating}/10
                          </Typography>
                        </Stack>
                        <LinearProgress 
                          variant="determinate" 
                          value={(item.rating / 10) * 100} 
                          sx={{ height: 8, borderRadius: 1 }}
                        />
                      </Box>
                    )
                  })}
                </Stack>
              </Box>
            )}

            {review.type === REVIEW_TYPE.Tag && (
              <Box>
                <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                  Raw tag payload
                </Typography>
                <Box
                  sx={{
                    bgcolor: (theme) => theme.palette.action.hover,
                    px: 2,
                    py: 1.5,
                    borderRadius: 2,
                    fontFamily: 'monospace',
                    fontSize: 13,
                    maxHeight: 200,
                    overflow: 'auto',
                  }}
                >
                  <pre style={{ margin: 0 }}>
                    {JSON.stringify(review.descriptionTag, null, 2)}
                  </pre>
                </Box>
              </Box>
            )}

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
