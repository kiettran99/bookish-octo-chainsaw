import { useEffect, useState } from 'react'
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
} from '@mui/material'

import {
  TAG_CATEGORY,
  TAG_CATEGORY_LABELS,
  type CreateTagRequestModel,
  type TagCategory,
  type TagResponseModel,
  type UpdateTagRequestModel,
} from '@/features/cine-review/tags/types'

interface TagDialogProps {
  open: boolean
  mode: 'create' | 'edit'
  tag?: TagResponseModel | null
  isSubmitting: boolean
  onClose: () => void
  onSubmit: (payload: CreateTagRequestModel | UpdateTagRequestModel) => void
}

type FormState = {
  name: string
  description: string
  category: TagCategory
  isActive: boolean
  displayOrder: number
}

const defaultState: FormState = {
  name: '',
  description: '',
  category: TAG_CATEGORY.Content,
  isActive: true,
  displayOrder: 0,
}

export function TagDialog({ open, mode, tag, isSubmitting, onClose, onSubmit }: TagDialogProps) {
  const [form, setForm] = useState<FormState>(defaultState)

  useEffect(() => {
    if (!tag) {
      setForm(defaultState)
      return
    }

    setForm({
      name: tag.name,
      description: tag.description ?? '',
      category: tag.category,
      isActive: tag.isActive,
      displayOrder: tag.displayOrder,
    })
  }, [tag])

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (mode === 'create') {
      const payload: CreateTagRequestModel = {
        name: form.name,
        description: form.description || undefined,
        category: form.category,
        displayOrder: form.displayOrder,
      }
      onSubmit(payload)
    } else if (tag) {
      const payload: UpdateTagRequestModel = {
        id: tag.id,
        name: form.name,
        description: form.description || undefined,
        category: form.category,
        isActive: form.isActive,
        displayOrder: form.displayOrder,
      }
      onSubmit(payload)
    }
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{mode === 'create' ? 'Create tag' : 'Update tag'}</DialogTitle>
      <DialogContent>
        <Stack component="form" spacing={2.5} sx={{ mt: 1 }} onSubmit={handleSubmit}>
          <TextField
            autoFocus
            required
            label="Name"
            value={form.name}
            onChange={(event) => setForm((prev) => ({ ...prev, name: event.target.value }))}
          />
          <TextField
            label="Description"
            value={form.description}
            multiline
            minRows={3}
            onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))}
          />
          <Select
            label="Category"
            value={form.category}
            onChange={(event) =>
              setForm((prev) => ({ ...prev, category: Number(event.target.value) as TagCategory }))
            }
            displayEmpty
          >
            {Object.values(TAG_CATEGORY).map((value) => (
              <MenuItem key={value} value={value}>
                {TAG_CATEGORY_LABELS[value as TagCategory]}
              </MenuItem>
            ))}
          </Select>
          <TextField
            label="Display order"
            type="number"
            value={form.displayOrder}
            onChange={(event) =>
              setForm((prev) => ({ ...prev, displayOrder: Number(event.target.value) }))
            }
          />
          {mode === 'edit' ? (
            <FormControlLabel
              control={
                <Switch
                  checked={form.isActive}
                  onChange={(event) =>
                    setForm((prev) => ({ ...prev, isActive: event.target.checked }))
                  }
                />
              }
              label="Active"
            />
          ) : null}

          <DialogActions sx={{ px: 0 }}>
            <Button onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save tag'}
            </Button>
          </DialogActions>
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
