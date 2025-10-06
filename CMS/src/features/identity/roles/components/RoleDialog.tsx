import { useEffect, useState } from 'react'
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'

import type { RoleModel } from '@/features/identity/roles/types'

interface RoleDialogProps {
  open: boolean
  mode: 'create' | 'edit'
  role?: RoleModel | null
  isSubmitting: boolean
  onClose: () => void
  onSubmit: (name: string) => void
}

export function RoleDialog({ open, mode, role, isSubmitting, onClose, onSubmit }: RoleDialogProps) {
  const [name, setName] = useState('')

  useEffect(() => {
    if (!role) {
      setName('')
    } else {
      setName(role.name)
    }
  }, [role])

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    onSubmit(name)
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{mode === 'create' ? 'Create role' : 'Update role'}</DialogTitle>
      <DialogContent>
        <Stack component="form" spacing={2} onSubmit={handleSubmit} sx={{ mt: 1 }}>
          <TextField
            autoFocus
            required
            label="Role name"
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
          <DialogActions sx={{ px: 0 }}>
            <Button onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save'}
            </Button>
          </DialogActions>
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
