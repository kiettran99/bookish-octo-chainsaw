import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
  Stack,
  Alert,
} from '@mui/material'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'

interface ApproveReviewDialogProps {
  open: boolean
  userName?: string | null
  isLoading?: boolean
  onClose: () => void
  onConfirm: () => void
}

export function ApproveReviewDialog({
  open,
  userName,
  isLoading = false,
  onClose,
  onConfirm,
}: ApproveReviewDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" spacing={1} alignItems="center">
          <CheckCircleRoundedIcon color="success" />
          <span>Approve Review</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Alert severity="success" sx={{ mb: 2 }}>
          This review will be published and visible to all users.
        </Alert>
        <Typography>
          Are you sure you want to approve this review from <strong>"{userName}"</strong>?
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isLoading}>
          Cancel
        </Button>
        <Button
          onClick={onConfirm}
          variant="contained"
          color="success"
          disabled={isLoading}
          startIcon={<CheckCircleRoundedIcon />}
        >
          {isLoading ? 'Approving…' : 'Approve Review'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
