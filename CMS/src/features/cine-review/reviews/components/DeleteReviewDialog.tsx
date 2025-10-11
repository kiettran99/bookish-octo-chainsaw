import { useState } from 'react'
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Alert,
  Stack,
} from '@mui/material'
import DeleteRoundedIcon from '@mui/icons-material/DeleteRounded'

interface DeleteReviewDialogProps {
  open: boolean
  userName?: string | null
  isLoading?: boolean
  onClose: () => void
  onConfirm: (rejectReason: string) => void
}

export function DeleteReviewDialog({
  open,
  userName,
  isLoading = false,
  onClose,
  onConfirm,
}: DeleteReviewDialogProps) {
  const [rejectReason, setRejectReason] = useState('')

  const handleConfirm = () => {
    onConfirm(rejectReason.trim())
  }

  const handleClose = () => {
    setRejectReason('')
    onClose()
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <DeleteRoundedIcon color="error" />
          <span>Delete Review</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="warning">
            Are you sure you want to delete this review from <strong>"{userName}"</strong>?
            This action will mark the review as deleted.
          </Alert>
          
          <TextField
            label="Reject Reason (optional)"
            placeholder="e.g., Spam, Inappropriate content, Violates guidelines..."
            multiline
            rows={3}
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            fullWidth
            disabled={isLoading}
            helperText="Provide a reason for rejecting this review. This will be visible to the user."
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={isLoading}>
          Cancel
        </Button>
        <Button
          onClick={handleConfirm}
          variant="contained"
          color="error"
          disabled={isLoading}
          startIcon={<DeleteRoundedIcon />}
        >
          {isLoading ? 'Deleting…' : 'Delete Review'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
