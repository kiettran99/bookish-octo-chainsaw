import { useEffect, useState } from 'react'
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Alert,
} from '@mui/material'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import SaveRoundedIcon from '@mui/icons-material/SaveRounded'

import {
  REVIEW_STATUS,
  REVIEW_STATUS_LABELS,
  REVIEW_TYPE,
  type ReviewResponseModel,
  type ReviewStatus,
} from '@/features/cine-review/reviews/types'

interface EditReviewDialogProps {
  open: boolean
  review: ReviewResponseModel | null
  isLoading?: boolean
  onClose: () => void
  onConfirm: (data: {
    reviewId: number
    rating: number
    description?: string
    status?: ReviewStatus
    rejectReason?: string
  }) => void
}

export function EditReviewDialog({
  open,
  review,
  isLoading = false,
  onClose,
  onConfirm,
}: EditReviewDialogProps) {
  const [rating, setRating] = useState<string>('')
  const [description, setDescription] = useState<string>('')
  const [status, setStatus] = useState<ReviewStatus | ''>('')
  const [rejectReason, setRejectReason] = useState<string>('')

  useEffect(() => {
    if (review && open) {
      setRating(review.rating.toString())
      setDescription(review.description || '')
      setStatus(review.status)
      setRejectReason(review.rejectReason || '')
    }
  }, [review, open])

  const handleConfirm = () => {
    if (!review) return

    const ratingNum = parseFloat(rating)
    if (isNaN(ratingNum) || ratingNum < 1 || ratingNum > 10) {
      return
    }

    onConfirm({
      reviewId: review.id,
      rating: ratingNum,
      description: review.type === REVIEW_TYPE.Normal ? description : undefined,
      status: status !== '' ? status : undefined,
      rejectReason: status === REVIEW_STATUS.Deleted && rejectReason ? rejectReason : undefined,
    })
  }

  const handleClose = () => {
    setRating('')
    setDescription('')
    setStatus('')
    setRejectReason('')
    onClose()
  }

  if (!review) return null

  const ratingNum = parseFloat(rating)
  const isRatingValid = !isNaN(ratingNum) && ratingNum >= 1 && ratingNum <= 10

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <EditRoundedIcon color="primary" />
          <span>Edit Review</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Alert severity="info">
            Editing review from <strong>{review.userName || `User #${review.userId}`}</strong>
          </Alert>

          <TextField
            label="Rating"
            type="number"
            value={rating}
            onChange={(e) => setRating(e.target.value)}
            fullWidth
            disabled={isLoading}
            inputProps={{ min: 1, max: 10, step: 0.1 }}
            error={!isRatingValid && rating !== ''}
            helperText={
              !isRatingValid && rating !== ''
                ? 'Rating must be between 1.0 and 10.0'
                : 'Rating on a scale of 1.0 to 10.0'
            }
          />

          {review.type === REVIEW_TYPE.Normal && (
            <TextField
              label="Description"
              multiline
              rows={4}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              fullWidth
              disabled={isLoading}
              helperText="Edit the review description (for Normal/Standard reviews only)"
            />
          )}

          <FormControl fullWidth>
            <InputLabel>Status</InputLabel>
            <Select
              value={status}
              label="Status"
              onChange={(e) => setStatus(e.target.value as ReviewStatus)}
              disabled={isLoading}
            >
              {Object.entries(REVIEW_STATUS_LABELS).map(([key, label]) => (
                <MenuItem key={key} value={Number(key)}>
                  {label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {status === REVIEW_STATUS.Deleted && (
            <TextField
              label="Reject Reason"
              placeholder="Provide a reason for deletion..."
              multiline
              rows={2}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              fullWidth
              disabled={isLoading}
              helperText="This reason will be visible to the user"
            />
          )}

          {review.type === REVIEW_TYPE.Tag && (
            <Alert severity="warning">
              Tag-based reviews cannot have their tags edited here. Only rating and status can be
              changed.
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={isLoading}>
          Cancel
        </Button>
        <Button
          onClick={handleConfirm}
          variant="contained"
          color="primary"
          disabled={isLoading || !isRatingValid}
          startIcon={<SaveRoundedIcon />}
        >
          {isLoading ? 'Saving…' : 'Save Changes'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
